using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OttoAura.AuraMove;

// MoveEffects: the shimmer tween and audio/visual bursts that play on every client that has the
// moved object loaded. Called from inside the RPC handler so the effect is network-visible without
// spawning any networked objects.
//
// Scale was chosen over an alpha fade deliberately. Valheim's piece shaders are opaque and there
// is no reliable _Color alpha to drive; a scale tween needs no shader assumptions, reverts
// exactly, and reads as a dematerialise. localScale is never serialised, so nothing can leak
// into the save.
internal static class MoveEffects
{
    // Shrink to this fraction of the original scale, never exactly zero so the transform stays
    // mathematically well-defined throughout the tween.
    private const float ScaleEpsilon = 0.02f;

    // One in-flight shimmer. The object reference is kept so StopAll can put the scale back.
    private sealed class Tween
    {
        internal Coroutine? Coroutine;
        internal Vector3 OriginalScale;
        internal GameObject? Obj;
    }

    // Keyed by instance ID so a second move on the same object cancels and cleans up the first
    // tween, even if it is mid-yield. The entry is registered before the coroutine starts: a
    // Shimmer Seconds of 0 runs the whole routine synchronously inside StartCoroutine, and an
    // entry written afterwards would never be cleared.
    private static readonly Dictionary<int, Tween> _active = new();

    // Guard so each unresolvable or networked fallback prefab name is warned about once only.
    private static readonly HashSet<string> _warnedNames = new();

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
        // outright, so anything that syncs its transform snaps instead. Both bursts still play.
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

    internal static void StopAll()
    {
        foreach (Tween tween in _active.Values)
        {
            Stop(tween);
        }
        _active.Clear();
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

        // Step 1: burst at the old spot.
        PlayBurst(movedObject, fromPosition, fromRotation);

        if (seconds > 0f)
        {
            // Step 2: shrink over the first half.
            float elapsed = 0f;
            while (elapsed < half)
            {
                if (movedObject == null)
                {
                    // Destroyed before apply() ran - do not call it; nothing to restore.
                    Finish(id, tween);
                    yield break;
                }
                elapsed += Time.deltaTime;
                movedObject.transform.localScale = Vector3.Lerp(originalScale, epsilon, Mathf.Clamp01(elapsed / half));
                yield return null;
            }

            if (movedObject == null)
            {
                Finish(id, tween);
                yield break;
            }
            movedObject.transform.localScale = epsilon;
        }

        // Step 3: apply the relocation at the midpoint.
        apply();

        if (seconds > 0f)
        {
            // Step 4: grow back over the second half.
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

        // Step 5: burst at the new spot, then restore the exact original scale.
        PlayBurst(movedObject, toPosition, toRotation);
        movedObject.transform.localScale = originalScale;
        Finish(id, tween);
    }

    private static void PlayBurst(GameObject movedObject, Vector3 position, Quaternion rotation)
    {
        // Preferred: the piece's own place effect is the exact sound and sparkle the game ships
        // for placing that piece, so nothing has to be guessed and nothing can be missing.
        Piece? piece = movedObject.GetComponent<Piece>();
        if (piece != null && HasUsablePlaceEffect(piece))
        {
            piece.m_placeEffect.Create(position, rotation);
            return;
        }

        // Fallback: comma-separated names from config, resolved by stable hash so a missing name
        // warns once instead of spamming ZNetScene.GetPrefab's error log.
        PlayFallbackBurst(position, rotation);
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

    private static void PlayFallbackBurst(Vector3 position, Quaternion rotation)
    {
        if (ZNetScene.instance == null)
        {
            return;
        }

        string csv = OttoAuraPlugin.AuraMoveEffectPrefabs.Value;
        if (string.IsNullOrWhiteSpace(csv))
        {
            return;
        }

        foreach (string raw in csv.Split(','))
        {
            string name = raw.Trim();
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            int hash = name.GetStableHashCode();
            if (!ZNetScene.instance.m_namedPrefabs.TryGetValue(hash, out GameObject prefab))
            {
                if (_warnedNames.Add(name))
                {
                    OttoAuraPlugin.OttoAuraLogger.LogWarning($"AuraMove: effect prefab '{name}' not found in ZNetScene; skipped.");
                }
                continue;
            }

            // A ZNetView on a fallback prefab would spawn one copy per client instead of one
            // total, populating the world with duplicates.
            if (prefab.GetComponent<ZNetView>() != null)
            {
                if (_warnedNames.Add(name))
                {
                    OttoAuraPlugin.OttoAuraLogger.LogWarning($"AuraMove: effect prefab '{name}' has a ZNetView and would duplicate across clients; skipped.");
                }
                continue;
            }

            UnityEngine.Object.Instantiate(prefab, position, rotation);
        }
    }
}
