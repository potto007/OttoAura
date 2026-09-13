using HarmonyLib;
using UnityEngine;

namespace OttoAura.AuraMove;

// The carry: which placed object the Merchant Guild has hold of, and the handful of vanilla
// placement hooks that make the game itself aim it.
//
// While carrying, PieceTable.GetSelectedPrefab hands vanilla the source object's own prefab, so
// Player.SetupPlacementGhost builds a real ghost of it and Player.UpdatePlacementGhost supplies
// rotation, snapping, ward checks, biome and clipping rules for free. AuraMove adds exactly one
// rule of its own, the Max Move Distance check, and refuses the placement itself in
// Player.TryPlacePiece so nothing is ever instantiated.
internal static class MoveCarry
{
    private static Piece? _source;
    private static GameObject? _carriedPrefab;
    private static string _carriedName = "";
    private static bool _tooFar;

    internal static bool IsCarrying => _source != null && _carriedPrefab != null;
    internal static Piece? Source => _source;
    internal static GameObject? CarriedPrefab => _carriedPrefab;
    internal static string CarriedName => _carriedName;

    // True when the ghost is further from the source than Max Move Distance allows. Only
    // meaningful while carrying and only after Player.UpdatePlacementGhost has run this frame.
    internal static bool TooFar => _tooFar;

    // Take hold of a placed object. The caller has already judged eligibility.
    internal static bool Grab(Player player, Piece piece)
    {
        if (ZNetScene.instance == null)
        {
            return false;
        }

        string prefabName = Utils.GetPrefabName(piece.gameObject.name);
        GameObject? prefab = ZNetScene.instance.GetPrefab(prefabName);
        if (prefab == null)
        {
            OttoAuraPlugin.OttoAuraLogger.LogWarning($"AuraMove: could not resolve prefab '{prefabName}', so the Guild cannot take hold of it.");
            return false;
        }

        _source = piece;
        _carriedPrefab = prefab;
        _carriedName = Localization.instance.Localize(piece.m_name);
        _tooFar = false;

        // Start the ghost facing the way the object already stands, the same rounding vanilla
        // uses when the hammer copies a piece.
        player.m_placeRotation = Mathf.RoundToInt(piece.transform.rotation.eulerAngles.y / player.m_placeRotationDegrees);

        // The Guild's summoning ring at the object's feet. Local only: nobody else has been told
        // about the grab, and nothing has been charged yet.
        MoveEffects.PlayGrab(piece);

        // Rebuild the ghost now that GetSelectedPrefab answers with the carried prefab.
        player.SetupPlacementGhost();
        return true;
    }

    // Let go. rebuildGhost puts the invisible Guild Move ghost back; pass false where vanilla is
    // about to rebuild it anyway (leaving place mode, changing category).
    internal static void Drop(bool rebuildGhost)
    {
        if (_source == null && _carriedPrefab == null)
        {
            return;
        }

        _source = null;
        _carriedPrefab = null;
        _carriedName = "";
        _tooFar = false;

        // The ring belongs to the carry, whether it ended in a cancel or in a move.
        MoveEffects.StopGrab();

        Player? player = Player.m_localPlayer;
        if (rebuildGhost && player != null && player.InPlaceMode())
        {
            player.SetupPlacementGhost();
        }
    }

    // Player.UpdatePlacementGhost has already decided the placement is fine; add the one rule
    // vanilla knows nothing about.
    internal static void ApplyDistanceRule(Player player)
    {
        _tooFar = false;

        if (!IsCarrying || player.m_placementGhost == null || _source == null)
        {
            return;
        }

        // A placement vanilla already refused keeps vanilla's reason, which is the more useful one.
        if (player.m_placementStatus != Player.PlacementStatus.Valid)
        {
            return;
        }

        float distance = Vector3.Distance(player.m_placementGhost.transform.position, _source.transform.position);
        if (distance <= OttoAuraPlugin.AuraMoveMaxDistance.Value)
        {
            return;
        }

        _tooFar = true;
        player.m_placementStatus = Player.PlacementStatus.Invalid;
        player.SetPlacementGhostValid(false);
    }
}

// While carrying, vanilla is asked to build and aim a ghost of the object being moved. The guard
// is deliberately narrow: the result is only swapped when the table itself answered with the
// Guild Move pseudo-piece, so no other piece table and no other selection is ever affected.
[HarmonyPatch(typeof(PieceTable), nameof(PieceTable.GetSelectedPrefab))]
static class PieceTable_GetSelectedPrefab_Patch
{
    static void Postfix(ref GameObject __result)
    {
        if (!MoveCarry.IsCarrying || __result == null)
        {
            return;
        }

        if (GuildMove.IsPseudoPiece(__result.GetComponent<Piece>()))
        {
            __result = MoveCarry.CarriedPrefab!;
        }
    }
}

// GetBuildSelection feeds the build panel. With the prefab swapped it would show the carried
// object's name and its build cost, which is not what the player is paying, so put the
// pseudo-piece back for the panel only.
[HarmonyPatch(typeof(Player), nameof(Player.GetBuildSelection))]
static class Player_GetBuildSelection_Patch
{
    static void Postfix(Player __instance, ref Piece go)
    {
        if (!MoveCarry.IsCarrying || __instance != Player.m_localPlayer || GuildMove.PseudoPiece == null)
        {
            return;
        }

        if (go != null && GuildMove.IsPseudoPiece(go))
        {
            return;
        }

        go = GuildMove.PseudoPiece;
    }
}

// Max Move Distance is AuraMove's own placement rule, applied after vanilla has had its say.
[HarmonyPatch(typeof(Player), nameof(Player.UpdatePlacementGhost))]
static class Player_UpdatePlacementGhost_Patch
{
    static void Postfix(Player __instance)
    {
        if (__instance != Player.m_localPlayer)
        {
            return;
        }

        MoveCarry.ApplyDistanceRule(__instance);
    }
}

// Left click with Guild Move selected. Nothing is ever built: the click either takes hold of an
// object or sets the carried one down through the relocation RPC.
[HarmonyPatch(typeof(Player), nameof(Player.TryPlacePiece))]
static class Player_TryPlacePiece_Patch
{
    static bool Prefix(Player __instance, Piece piece, ref bool __result)
    {
        if (__instance != Player.m_localPlayer || !GuildMove.IsPseudoPiece(piece))
        {
            return true;
        }

        __result = false;

        // A true return hands the click back to vanilla, which reports why the destination was
        // refused and returns false itself. It can never reach the placement branch: that branch
        // only runs on a valid placement, and AuraMoveController has already taken those.
        return AuraMoveController.HandlePlaceClick(__instance);
    }
}

// Right click is the hammer's remove. While the Guild has hold of something it means "let go"
// instead, so a cancel cannot smash whatever the player happens to be pointing at.
[HarmonyPatch(typeof(Player), nameof(Player.RemovePiece))]
static class Player_RemovePiece_Patch
{
    static bool Prefix(Player __instance, ref bool __result)
    {
        if (__instance != Player.m_localPlayer || !MoveCarry.IsCarrying)
        {
            return true;
        }

        AuraMoveController.CancelByPlayer(__instance);
        __result = false;
        return false;
    }
}

// Selecting another piece, another category, or another tool drops the carry. These are prefixes
// because each of them goes on to rebuild the placement ghost, and the carry has to be gone
// before that happens or vanilla builds a ghost of the object the player just stopped moving.
[HarmonyPatch(typeof(Player), nameof(Player.SetSelectedPiece), typeof(Vector2Int))]
static class Player_SetSelectedPiece_Patch
{
    static void Prefix() => MoveCarry.Drop(rebuildGhost: true);
}

[HarmonyPatch(typeof(Player), nameof(Player.SetBuildCategory), typeof(int))]
static class Player_SetBuildCategoryIndex_Patch
{
    static void Prefix() => MoveCarry.Drop(rebuildGhost: false);
}

[HarmonyPatch(typeof(Player), nameof(Player.SetBuildCategory), typeof(Piece.PieceCategory))]
static class Player_SetBuildCategory_Patch
{
    static void Prefix() => MoveCarry.Drop(rebuildGhost: false);
}

// SetPlaceMode runs on every equipment change, so it covers putting the hammer away, swapping to
// another build tool, and death, all of which end the carry.
[HarmonyPatch(typeof(Player), nameof(Player.SetPlaceMode))]
static class Player_SetPlaceMode_Patch
{
    static void Prefix() => MoveCarry.Drop(rebuildGhost: false);
}

// Belt and braces. The pseudo-piece is not a thing that can exist in the world, so it is never
// instantiated, whatever else goes wrong upstream.
[HarmonyPatch(typeof(Player), nameof(Player.PlacePiece))]
static class Player_PlacePiece_Patch
{
    static bool Prefix(Piece piece)
    {
        if (!GuildMove.IsPseudoPiece(piece))
        {
            return true;
        }

        OttoAuraPlugin.OttoAuraLogger.LogWarning("AuraMove: something tried to build the Guild Move pseudo-piece. Refused.");
        return false;
    }
}
