using System.Collections;
using OttoAura.AuraMove;
using UnityEngine;

namespace OttoAura.AuraTrade;

// TradeEffects: sold valuables leave the way AuraMove's objects do. A copy of the most valuable
// stack appears in front of the player as the depart effects flare, shrinks away while a wisp
// carries it along an arc into the ward, and the ward answers with the arrival effects.
//
// Local and cosmetic only. By the time this starts the items are out of the inventory and the
// coins are in the balance. Every instance goes through MoveEffects, so it is spawned with
// ZNetView.m_forceDisableInit, tracked, and reaped on the same five-second ceiling.
internal static class TradeEffects
{
    private const float ScaleEpsilon = 0.02f;
    private const float ArcHeight = 1.5f;
    private const float SpinDegreesPerSecond = 360f;

    internal static void Play(Player player, PrivateArea ward, ItemDrop.ItemData item)
    {
        Transform transform = player.transform;
        Vector3 from = transform.position + Vector3.up * 1.3f + transform.forward * 0.7f;
        Vector3 to = ward.transform.position + Vector3.up * 1.5f;

        MoveEffects.Spawn(OttoAuraPlugin.AuraTradeDepartEffects.Value, from, Quaternion.identity, isGrab: false);

        float seconds = Mathf.Max(0f, OttoAuraPlugin.AuraTradeShimmerSeconds.Value);
        if (seconds <= 0f)
        {
            MoveEffects.Spawn(OttoAuraPlugin.AuraTradeArriveEffects.Value, to, Quaternion.identity, isGrab: false);
            return;
        }

        GameObject? visual = item.m_dropPrefab != null
            ? MoveEffects.SpawnOne(item.m_dropPrefab.name, from, Quaternion.identity, isGrab: false)
            : null;
        if (visual != null)
        {
            MakeInert(visual);
        }

        GameObject? wisp = MoveEffects.SpawnOne(
            MoveEffects.FirstName(OttoAuraPlugin.AuraTradeTravelEffect.Value), from, Quaternion.identity, isGrab: false);

        OttoAuraPlugin.context.StartCoroutine(Carry(visual, wisp, from, to, seconds));
    }

    // The copy is a real item prefab. With its network view gone it cannot be picked up, but its
    // rigidbody would still fall and its collider would still push the player, so both are stilled.
    private static void MakeInert(GameObject visual)
    {
        foreach (Rigidbody body in visual.GetComponentsInChildren<Rigidbody>())
        {
            body.isKinematic = true;
            body.detectCollisions = false;
        }

        foreach (Collider collider in visual.GetComponentsInChildren<Collider>())
        {
            collider.enabled = false;
        }

        foreach (MonoBehaviour behaviour in visual.GetComponentsInChildren<MonoBehaviour>())
        {
            if (behaviour is ItemDrop || behaviour is Floating)
            {
                behaviour.enabled = false;
            }
        }
    }

    private static IEnumerator Carry(GameObject? visual, GameObject? wisp, Vector3 from, Vector3 to, float seconds)
    {
        Vector3 scale = visual != null ? visual.transform.localScale : Vector3.one;
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / seconds);
            Vector3 along = Vector3.Lerp(from, to, t);
            along.y += ArcHeight * Mathf.Sin(t * Mathf.PI);

            // MoveEffects.StopAll can destroy either instance mid-flight; Unity's null check sees it.
            if (visual != null)
            {
                visual.transform.position = along;
                visual.transform.localScale = Vector3.Lerp(scale, scale * ScaleEpsilon, t);
                visual.transform.Rotate(0f, SpinDegreesPerSecond * Time.deltaTime, 0f);
            }

            if (wisp != null)
            {
                wisp.transform.position = along;
            }

            yield return null;
        }

        MoveEffects.DestroyTracked(visual);
        MoveEffects.DestroyTracked(wisp);
        MoveEffects.Spawn(OttoAuraPlugin.AuraTradeArriveEffects.Value, to, Quaternion.identity, isGrab: false);
    }
}
