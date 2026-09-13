using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OttoAura.AuraMove;

// MoveEffects: the Guild's conjuring. Five stages, each a comma-separated list of vanilla effect
// prefab names from config, so the whole choreography is overridable without a rebuild.
//
//   Grab    local only, when the player takes hold: a summoning ring and a soft cast.
//   Depart  every client, at the old spot, as the object starts to shrink.
//   Travel  every client, one wisp lerped along an arc from the old spot to the new one.
//   Arrive  every client, at the new spot, as the object grows back in.
//   Finish  every client, at the new spot, when the object is whole again.
//
// Everything but Grab runs from inside the RPC handler, so every client that has the object loaded
// sees the same conjuring, not only the player who paid.
//
// Scale was chosen over an alpha fade deliberately. Valheim's piece shaders are opaque and there
// is no reliable _Color alpha to drive; a scale tween needs no shader assumptions, reverts
// exactly, and reads as a dematerialise. localScale is never serialised, so nothing can leak
// into the save.
//
// Nothing spawned here is ever parented to the moved object. An attached effect would be dragged
// through the relocation and would inherit the shimmer's near-zero scale, so every effect is a
// free-standing world instance at a fixed position, and this class owns its lifetime rather than
// trusting the prefab's own TimedDestruction (which is a no-op once its ZNetView is gone).
internal static class MoveEffects
{
    // Shrink to this fraction of the original scale, never exactly zero so the transform stays
    // mathematically well-defined throughout the tween.
    private const float ScaleEpsilon = 0.02f;

    // Hard ceiling on any effect instance we spawn. Vanilla one-shots are all well under this;
    // the cap exists so a looping or attach-only prefab someone puts in the config cannot pile up.
    private const float EffectLifetime = 5f;

    // How high above the straight line the travelling wisp arcs, as a fraction of the distance
    // covered, and the ceiling on that in metres so a long move does not launch it into orbit.
    private const float ArcFraction = 0.25f;
    private const float ArcMaxHeight = 3f;

    // One in-flight shimmer. The object reference is kept so StopAll can put the scale back.
    private sealed class Tween
    {
        internal Coroutine? Coroutine;
        internal Vector3 OriginalScale;
        internal GameObject? Obj;
    }

    // One spawned effect instance and the moment it must be gone by.
    private struct Spawned
    {
        internal GameObject Obj;
        internal float ExpiresAt;
        internal bool IsGrab;
    }

    // Keyed by instance ID so a second move on the same object cancels and cleans up the first
    // tween, even if it is mid-yield. The entry is registered before the coroutine starts: a
    // Shimmer Seconds of 0 runs the whole routine synchronously inside StartCoroutine, and an
    // entry written afterwards would never be cleared.
    private static readonly Dictionary<int, Tween> _active = new();

    // Every effect instance this class has spawned and not yet destroyed. Reaped from Tick.
    private static readonly List<Spawned> _spawned = new();

    // Guard so each unresolvable effect prefab name is warned about once only.
    private static readonly HashSet<string> _warnedNames = new();

    // Reap expired effect instances. Called every frame from AuraMoveController.Tick, before its
    // own local-player guard, so cleanup keeps running while the player is being torn down.
    internal static void Tick()
    {
        float now = Time.time;
        for (int i = _spawned.Count - 1; i >= 0; i--)
        {
            Spawned entry = _spawned[i];
            if (entry.Obj == null)
            {
                _spawned.RemoveAt(i);
                continue;
            }

            if (now >= entry.ExpiresAt)
            {
                UnityEngine.Object.Destroy(entry.Obj);
                _spawned.RemoveAt(i);
            }
        }
    }

    // Stage 1. The player has just taken hold of something. Local only: nobody else has been told
    // about the grab yet, and nothing has been charged.
    internal static void PlayGrab(Piece source)
    {
        StopGrab();
        // Rings and circles want to lie flat on the ground, so they are spawned world-aligned
        // rather than following the piece's yaw.
        Spawn(OttoAuraPlugin.AuraMoveGrabEffects.Value, source.transform.position, Quaternion.identity, isGrab: true);
    }

    // The grab ended, by cancel or by confirm. Take the summoning ring away with it.
    internal static void StopGrab()
    {
        for (int i = _spawned.Count - 1; i >= 0; i--)
        {
            if (!_spawned[i].IsGrab)
            {
                continue;
            }

            if (_spawned[i].Obj != null)
            {
                UnityEngine.Object.Destroy(_spawned[i].Obj);
            }
            _spawned.RemoveAt(i);
        }
    }

    internal static void PlayRelocation(
        GameObject movedObject,
        Vector3 fromPosition,
        Quaternion fromRotation,
        Vector3 toPosition,
        Quaternion toRotation,
        Action apply)
    {
        int id = movedObject.GetInstanceID();

        // Cancel any in-flight tween for the same object. Restore its scale first so the new
        // tween starts from the real dimensions, not wherever the old one stopped.
        if (_active.TryGetValue(id, out Tween? existing))
        {
            Stop(existing);
            _active.Remove(id);
        }

        float seconds = Mathf.Max(0f, OttoAuraPlugin.AuraMoveShimmerSeconds.Value);

        // The ZDO is written the moment the RPC lands, but ZSyncTransform.OwnerSync writes the
        // live transform straight back into the ZDO every frame on the owning client. Holding
        // the object at the old spot for the first half of a tween would let that undo the move
        // outright, so anything that syncs its transform snaps instead. Every stage still fires.
        if (seconds > 0f && SyncsTransform(movedObject))
        {
            seconds = 0f;
        }

        Tween tween = new() { OriginalScale = movedObject.transform.localScale, Obj = movedObject };
        _active[id] = tween;

        tween.Coroutine = OttoAuraPlugin.context.StartCoroutine(
            ShimmerRoutine(id, tween, movedObject, tween.OriginalScale, fromPosition, fromRotation,
                toPosition, toRotation, apply, seconds));
    }

    // True when the object keeps its ZDO transform in step with the live one every frame.
    private static bool SyncsTransform(GameObject movedObject) =>
        movedObject.TryGetComponent(out ZSyncTransform sync) && (sync.m_syncPosition || sync.m_syncRotation);

    // Shutdown, logout, and anything else that tears the world down under a shimmer.
    internal static void StopAll()
    {
        foreach (Tween tween in _active.Values)
        {
            Stop(tween);
        }
        _active.Clear();

        foreach (Spawned entry in _spawned)
        {
            if (entry.Obj != null)
            {
                UnityEngine.Object.Destroy(entry.Obj);
            }
        }
        _spawned.Clear();
    }

    // Halt a tween and put the object back to the size it started at.
    private static void Stop(Tween tween)
    {
        if (tween.Coroutine != null)
        {
            OttoAuraPlugin.context.StopCoroutine(tween.Coroutine);
        }

        if (tween.Obj != null)
        {
            tween.Obj.transform.localScale = tween.OriginalScale;
        }
    }

    // Drop the tween's registration, unless a newer tween for the same object already replaced it.
    private static void Finish(int id, Tween tween)
    {
        if (_active.TryGetValue(id, out Tween? current) && current == tween)
        {
            _active.Remove(id);
        }
    }

    private static IEnumerator ShimmerRoutine(
        int id,
        Tween tween,
        GameObject movedObject,
        Vector3 originalScale,
        Vector3 fromPosition,
        Quaternion fromRotation,
        Vector3 toPosition,
        Quaternion toRotation,
        Action apply,
        float seconds)
    {
        float half = seconds / 2f;
        Vector3 epsilon = originalScale * ScaleEpsilon;

        // Stage 2: the object lets go of the old spot.
        Spawn(OttoAuraPlugin.AuraMoveDepartEffects.Value, fromPosition, Quaternion.identity, isGrab: false);

        if (seconds > 0f)
        {
            // Stage 3: the wisp crosses while the object shrinks, so it lands exactly as the
            // object reappears. Its lifetime is capped like everything else, in case this
            // routine is stopped mid-flight before it can destroy the wisp itself.
            GameObject? wisp = SpawnOne(
                FirstName(OttoAuraPlugin.AuraMoveTravelEffect.Value), fromPosition, Quaternion.identity, isGrab: false);

            float arc = Mathf.Min(Vector3.Distance(fromPosition, toPosition) * ArcFraction, ArcMaxHeight);

            float elapsed = 0f;
            while (elapsed < half)
            {
                if (movedObject == null)
                {
                    // Destroyed before apply() ran - do not call it; nothing to restore.
                    DestroyTracked(wisp);
                    Finish(id, tween);
                    yield break;
                }

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / half);
                movedObject.transform.localScale = Vector3.Lerp(originalScale, epsilon, t);

                if (wisp != null)
                {
                    // Straight line plus a sine hump, so the wisp leaves and lands on the ground
                    // and rides over whatever is between. No allocation per frame.
                    Vector3 along = Vector3.Lerp(fromPosition, toPosition, t);
                    along.y += arc * Mathf.Sin(t * Mathf.PI);
                    wisp.transform.position = along;
                }

                yield return null;
            }

            DestroyTracked(wisp);

            if (movedObject == null)
            {
                Finish(id, tween);
                yield break;
            }
            movedObject.transform.localScale = epsilon;
        }

        // Apply the relocation at the midpoint, then announce the arrival at the new spot.
        apply();

        Spawn(OttoAuraPlugin.AuraMoveArriveEffects.Value, toPosition, Quaternion.identity, isGrab: false);

        // The piece's own place effect is the exact sound and sparkle the game ships for placing
        // that piece, so it rides along with the arrival where the piece has one. Vanilla owns the
        // lifetime of what Create spawns, exactly as it does when the piece is built by hand.
        Piece? piece = movedObject.GetComponent<Piece>();
        if (piece != null && HasUsablePlaceEffect(piece))
        {
            piece.m_placeEffect.Create(toPosition, toRotation);
        }

        if (seconds > 0f)
        {
            // Stage 4 continued: grow back over the second half.
            float elapsed = 0f;
            while (elapsed < half)
            {
                if (movedObject == null)
                {
                    // Destroyed after apply() ran; nothing left to restore.
                    Finish(id, tween);
                    yield break;
                }
                elapsed += Time.deltaTime;
                movedObject.transform.localScale = Vector3.Lerp(epsilon, originalScale, Mathf.Clamp01(elapsed / half));
                yield return null;
            }

            if (movedObject == null)
            {
                Finish(id, tween);
                yield break;
            }
        }

        // Stage 5: the object is whole again.
        Spawn(OttoAuraPlugin.AuraMoveFinishEffects.Value, toPosition, Quaternion.identity, isGrab: false);
        movedObject.transform.localScale = originalScale;
        Finish(id, tween);
    }

    private static bool HasUsablePlaceEffect(Piece piece)
    {
        if (piece.m_placeEffect?.m_effectPrefabs == null)
        {
            return false;
        }
        foreach (EffectList.EffectData entry in piece.m_placeEffect.m_effectPrefabs)
        {
            if (entry.m_enabled && entry.m_prefab != null)
            {
                return true;
            }
        }
        return false;
    }

    // Travel is a single prefab. Take the first name if somebody pastes a list into it anyway,
    // rather than looking up the whole string and warning about a name nobody typed.
    private static string FirstName(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return "";
        }

        int comma = csv.IndexOf(',');
        return (comma < 0 ? csv : csv.Substring(0, comma)).Trim();
    }

    // Spawn every prefab named in one comma-separated config entry.
    private static void Spawn(string csv, Vector3 position, Quaternion rotation, bool isGrab)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return;
        }

        foreach (string raw in csv.Split(','))
        {
            SpawnOne(raw.Trim(), position, rotation, isGrab);
        }
    }

    // Resolve one effect prefab by name and instantiate it purely locally.
    //
    // Names are looked up in ZNetScene.m_namedPrefabs by stable hash rather than through
    // GetPrefab, so a name that is not in this game version warns once instead of spamming
    // GetPrefab's error log every move.
    //
    // Many of the magic-flavoured vanilla effects (the summon and guardstone families in
    // particular) carry a ZNetView, and instantiating one of those as-is would create a networked
    // object once per client. ZNetView.m_forceDisableInit makes ZNetView.Awake destroy itself
    // instead of claiming a ZDO, which is how vanilla itself spawns preview-only copies, so the
    // instance is local, silent on the wire, and identical to look at.
    private static GameObject? SpawnOne(string name, Vector3 position, Quaternion rotation, bool isGrab)
    {
        if (string.IsNullOrEmpty(name) || ZNetScene.instance == null)
        {
            return null;
        }

        if (!ZNetScene.instance.m_namedPrefabs.TryGetValue(name.GetStableHashCode(), out GameObject prefab)
            || prefab == null)
        {
            if (_warnedNames.Add(name))
            {
                OttoAuraPlugin.OttoAuraLogger.LogWarning($"AuraMove: effect prefab '{name}' not found in ZNetScene; skipped.");
            }
            return null;
        }

        GameObject instance;
        bool previous = ZNetView.m_forceDisableInit;
        ZNetView.m_forceDisableInit = true;
        try
        {
            instance = UnityEngine.Object.Instantiate(prefab, position, rotation);
        }
        finally
        {
            ZNetView.m_forceDisableInit = previous;
        }

        _spawned.Add(new Spawned
        {
            Obj = instance,
            ExpiresAt = Time.time + EffectLifetime,
            IsGrab = isGrab,
        });

        return instance;
    }

    // Destroy one tracked instance early and forget it.
    private static void DestroyTracked(GameObject? instance)
    {
        if (instance == null)
        {
            return;
        }

        for (int i = _spawned.Count - 1; i >= 0; i--)
        {
            if (_spawned[i].Obj == instance)
            {
                _spawned.RemoveAt(i);
                break;
            }
        }

        UnityEngine.Object.Destroy(instance);
    }
}
