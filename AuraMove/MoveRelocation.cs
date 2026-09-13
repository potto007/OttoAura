using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace OttoAura.AuraMove;

// ZDO relocation: registers the RPC, sends it from the initiating client, handles it on every
// client, and applies all the bookkeeping a moved piece needs (WearNTear, EffectArea, Bed spawn,
// ArmorStand pose). MoveEffects owns the shimmer coroutine that wraps the visible change.
//
// The authority for the move is the ZDO, not the visual. Clients without OttoAura see the object
// jump rather than shimmer, but state and contents survive because the ZDO is the source of truth.
internal static class MoveRelocation
{
    internal const string RpcName = "OttoAura_MovePiece";

    // Register the RPC once per session on Game.Start. Skipped on dedicated servers because
    // OttoAura is a client feature and servers hold no instances to animate.
    // ZRoutedRpc.Register uses Dictionary.Add and throws on a duplicate, so guard with
    // ContainsKey to survive a second Game.Start in one process (e.g. returning to menu).
    internal static void Register()
    {
        if (ZNet.instance == null || ZNet.instance.IsDedicated())
        {
            return;
        }

        int hash = RpcName.GetStableHashCode();
        if (ZRoutedRpc.instance.m_functions.ContainsKey(hash))
        {
            return;
        }

        ZRoutedRpc.instance.Register<ZDOID, Vector3, Quaternion>(RpcName, RPC_MovePiece);
    }

    // True when Request has everything it needs. The controller calls this before taking the fee,
    // so a piece or a network layer that went away cannot cost the player coins for nothing.
    internal static bool CanRequest(Piece? piece) =>
        ZRoutedRpc.instance != null && piece != null && piece.m_nview != null && piece.m_nview.IsValid();

    // Send the relocation RPC to every client. The fee has already been charged by the caller.
    // Claims ownership so the ZDO write lands on this client; returns false on any missing dependency.
    internal static bool Request(Piece piece, Vector3 position, Quaternion rotation)
    {
        if (!CanRequest(piece))
        {
            OttoAuraPlugin.OttoAuraLogger.LogWarning("AuraMove: the piece or the network layer went away before the move could be sent.");
            return false;
        }

        piece.m_nview.ClaimOwnership();
        ZRoutedRpc.instance.InvokeRoutedRPC(
            ZRoutedRpc.Everybody,
            RpcName,
            piece.m_nview.GetZDO().m_uid,
            position,
            AvoidIdentity(rotation));

        return true;
    }

    // RPC handler - runs on every client that has OttoAura installed.
    // Clients without the mod ignore the unknown RPC name and pick the move up from the ZDO when
    // the zone reloads.
    private static void RPC_MovePiece(long sender, ZDOID zdoID, Vector3 position, Quaternion rotation)
    {
        // A routed RPC can still be in flight while the world is being torn down.
        if (ZDOMan.instance == null || ZNetScene.instance == null)
        {
            return;
        }

        ZDO? zdo = ZDOMan.instance.GetZDO(zdoID);
        if (zdo == null || !zdo.IsValid())
        {
            return;
        }

        // Only the owner writes the ZDO so the authority is a single client.
        if (zdo.IsOwner())
        {
            zdo.SetRotation(rotation);
            zdo.SetPosition(position);
        }

        // FindInstance returns null when the object is in an unloaded zone. That is normal: the
        // client will build the object at the new ZDO position when the zone loads, so nothing to do.
        ZNetView? nview = ZNetScene.instance.FindInstance(zdo);
        if (nview == null)
        {
            OttoAuraPlugin.OttoAuraLogger.LogDebug($"AuraMove: RPC_MovePiece: ZDO {zdoID} not loaded on this client, will build at new position from ZDO.");
            return;
        }

        // Capture the old transform before handing off to the effects layer, which may tween.
        Vector3 oldPosition = nview.gameObject.transform.position;
        Quaternion oldRotation = nview.gameObject.transform.rotation;

        // MoveEffects owns the shimmer coroutine and calls UpdateOrientation at the midpoint.
        MoveEffects.PlayRelocation(
            nview.gameObject,
            oldPosition,
            oldRotation,
            position,
            rotation,
            () => UpdateOrientation(nview.gameObject, position, rotation));
    }

    // Apply the new position and rotation to the live instance and run all the vanilla bookkeeping
    // that a relocated piece needs. Ported from OttoRedecorate.Redecorate.UpdateOrientation and its
    // MoveWearNTear/MoveEffectAreaBounds helpers (OnRedecorateBefore/After hooks omitted - those
    // are OttoRedecorate's own protocol and do not belong here).
    internal static void UpdateOrientation(GameObject movedObject, Vector3 position, Quaternion rotation)
    {
        // Capture the bed spawn point BEFORE the position change. If this bed was the local
        // player's active anchor we need to compare against the old spawn point, not the new one.
        Bed? anchorBed = null;
        if (movedObject.TryGetComponent(out Bed bed))
        {
            PlayerProfile profile = Game.instance.GetPlayerProfile();
            if (bed.GetOwner() == profile.GetPlayerID() && profile.GetCustomSpawnPoint() == bed.GetSpawnPoint())
            {
                anchorBed = bed;
            }
        }

        movedObject.transform.SetPositionAndRotation(position, rotation);

        // Re-set the player's custom spawn point if this bed was their anchor.
        if (anchorBed != null)
        {
            Game.instance.GetPlayerProfile().SetCustomSpawnPoint(anchorBed.GetSpawnPoint());
        }

        // WearNTear: re-run placement setup and re-subscribe to the new heightmap so structural
        // support is recalculated from the new position. Heightmap.FindHeightmap returns null
        // where no zone is loaded, so both sides of the re-subscribe must be null-guarded.
        if (movedObject.TryGetComponent(out WearNTear wnt))
        {
            wnt.OnPlaced();
            wnt.ClearCachedSupport();
            wnt.m_colliders = null;

            if (wnt.m_connectedHeightMap != null)
            {
                wnt.m_connectedHeightMap.m_clearConnectedWearNTearCache -= wnt.ClearCachedSupport;
            }

            wnt.m_connectedHeightMap = Heightmap.FindHeightmap(movedObject.transform.position);

            if (wnt.m_connectedHeightMap != null)
            {
                wnt.m_connectedHeightMap.m_clearConnectedWearNTearCache += wnt.ClearCachedSupport;
            }
        }

        // EffectArea: remove and re-add the cached area bounds so fire and monster-free zones
        // move with the object. Only re-add when Remove returned true (the area was registered).
        foreach (EffectArea ea in movedObject.GetComponentsInChildren<EffectArea>(true))
        {
            if (ea.m_type.HasFlag(EffectArea.Type.Burning))
            {
                if (EffectArea.s_BurningAreas.Remove(ea.burnCloseToArea))
                {
                    Bounds updated = ea.burnCloseToArea.Key;
                    updated.center = ea.m_collider.bounds.center;
                    ea.burnCloseToArea = new KeyValuePair<Bounds, EffectArea>(updated, ea);
                    EffectArea.s_BurningAreas.Add(ea.burnCloseToArea);
                }
            }

            if (ea.m_type.HasFlag(EffectArea.Type.NoMonsters))
            {
                if (EffectArea.s_noMonsterAreas.Remove(ea.noMonsterArea))
                {
                    Bounds updated = ea.noMonsterArea.Key;
                    updated.center = ea.m_collider.bounds.center;
                    ea.noMonsterArea = new KeyValuePair<Bounds, EffectArea>(updated, ea);
                    EffectArea.s_noMonsterAreas.Add(ea.noMonsterArea);
                }

                if (EffectArea.s_noMonsterCloseToAreas.Remove(ea.noMonsterCloseToArea))
                {
                    Bounds updated = ea.noMonsterCloseToArea.Key;
                    updated.center = ea.m_collider.bounds.center;
                    ea.noMonsterCloseToArea = new KeyValuePair<Bounds, EffectArea>(updated, ea);
                    EffectArea.s_noMonsterCloseToAreas.Add(ea.noMonsterCloseToArea);
                }
            }
        }

        // ArmorStand: poke the pose animator so the stand renders its equipment after the move.
        // Without this it goes blank until the next pose change. Done locally, not through
        // RPC_SetPose: every client runs this handler, so the RPC would send one message per
        // client to the owner and make the owner replay the pose effect once per client.
        // ArmorStand.Awake seeds m_pose from the ZDO, so the local value is the right one.
        if (movedObject.TryGetComponent(out ArmorStand armorStand))
        {
            armorStand.SetPose(armorStand.m_pose, effect: false);
        }
    }

    // ZDO.Serialize only writes m_rotation when it differs from Quaternion.identity.eulerAngles.
    // A piece rotated back to exactly identity would never have its rotation synced, leaving remote
    // clients with the stale pre-move rotation. Nudging the yaw by 0.01 degrees sets the dirty flag
    // and is invisible in game. (OttoRedecorate fixes this with a transpiler on ZDO.Serialize;
    // AuraMove does not patch anything and uses this nudge instead.)
    internal static Quaternion AvoidIdentity(Quaternion rotation)
    {
        Vector3 e = rotation.eulerAngles;
        if (Mathf.Abs(e.x) < 0.001f && Mathf.Abs(e.y) < 0.001f && Mathf.Abs(e.z) < 0.001f)
        {
            return Quaternion.Euler(0f, 0.01f, 0f);
        }

        return rotation;
    }
}

// Register the RPC after Game.Start so ZRoutedRpc.instance is alive and ready.
[HarmonyPatch(typeof(Game), nameof(Game.Start))]
static class Game_Start_Patch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        MoveRelocation.Register();
    }
}
