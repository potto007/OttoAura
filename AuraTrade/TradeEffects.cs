using System.Collections;
using HarmonyLib;
using OttoAura.AuraMove;
using UnityEngine;

namespace OttoAura.AuraTrade;

// TradeEffects: sold valuables leave the way AuraMove's objects do. A copy of the sold item
// appears in front of the seller as the depart effects flare, shrinks away while a wisp carries it
// along an arc into the ward, and the ward answers with the arrival effects.
//
// Every player sees it. The seller sends one routed RPC carrying the two points and the item's
// prefab name, and every client with OttoAura plays the same conjuring from it. Clients without
// the mod ignore the unknown RPC. The sale itself never rides on this message: by the time it is
// sent the items are out of the inventory and the coins are in the balance.
//
// Every instance goes through MoveEffects, so it is spawned with ZNetView.m_forceDisableInit,
// stays local to each client, is tracked, and is reaped on the same five-second ceiling.
internal static class TradeEffects
{
    internal const string RpcName = "OttoAura_TradeEffect";

    private const float ScaleEpsilon = 0.02f;
    private const float ArcHeight = 1.5f;
    private const float SpinDegreesPerSecond = 360f;

    // A sale further away than this is not played. Nobody can see it, and the item copy would
    // spawn into a zone this client may not even have loaded.
    private const float AudibleRange = 100f;

    // Once per session on Game.Start, like AuraMove's RPC. Skipped on dedicated servers, which
    // have nothing to draw. ZRoutedRpc.Register throws on a duplicate, so a second Game.Start in
    // one process (back to the menu and in again) is guarded.
    internal static void Register()
    {
        if (ZNet.instance == null || ZNet.instance.IsDedicated() || ZRoutedRpc.instance == null)
        {
            return;
        }

        if (ZRoutedRpc.instance.m_functions.ContainsKey(RpcName.GetStableHashCode()))
        {
            return;
        }

        ZRoutedRpc.instance.Register<Vector3, Vector3, string>(RpcName, RPC_TradeEffect);
    }

    // The seller plays its own copy directly and the RPC handler skips messages from itself, so
    // the seller sees the sale exactly once whether or not a broadcast is delivered locally.
    internal static void Broadcast(Player seller, PrivateArea ward, ItemDrop.ItemData item)
    {
        Transform transform = seller.transform;
        Vector3 from = transform.position + Vector3.up * 1.3f + transform.forward * 0.7f;
        Vector3 to = ward.transform.position + Vector3.up * 1.5f;
        string prefab = item.m_dropPrefab != null ? item.m_dropPrefab.name : "";

        Play(from, to, prefab);

        if (ZRoutedRpc.instance != null)
        {
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcName, from, to, prefab);
        }
    }

    private static void RPC_TradeEffect(long sender, Vector3 from, Vector3 to, string prefab)
    {
        // A routed RPC can still be in flight while the world is being torn down.
        if (ZRoutedRpc.instance == null || sender == ZRoutedRpc.instance.m_id || ZNetScene.instance == null)
        {
            return;
        }

        Player? local = Player.m_localPlayer;
        if (local == null || Vector3.Distance(local.transform.position, from) > AudibleRange)
        {
            return;
        }

        Play(from, to, prefab);
    }

    private static void Play(Vector3 from, Vector3 to, string prefab)
    {
        MoveEffects.Spawn(OttoAuraPlugin.AuraTradeDepartEffects.Value, from, Quaternion.identity, isGrab: false);

        float seconds = Mathf.Max(0f, OttoAuraPlugin.AuraTradeShimmerSeconds.Value);
        if (seconds <= 0f)
        {
            MoveEffects.Spawn(OttoAuraPlugin.AuraTradeArriveEffects.Value, to, Quaternion.identity, isGrab: false);
            return;
        }

        GameObject? visual = MoveEffects.SpawnOne(prefab, from, Quaternion.identity, isGrab: false);
        if (visual != null)
        {
            MakeInert(visual);
        }

        GameObject? wisp = MoveEffects.SpawnOne(
            MoveEffects.FirstName(OttoAuraPlugin.AuraTradeTravelEffect.Value), from, Quaternion.identity, isGrab: false);

        OttoAuraPlugin.context.StartCoroutine(Carry(visual, wisp, from, to, seconds));
    }

    // The copy is a real item prefab. With its network view gone it cannot be picked up, but its
    // rigidbody would still fall and its collider would still push players, so both are stilled.
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

// Registered after Game.Start so ZRoutedRpc.instance is alive, the same point AuraMove uses.
[HarmonyPatch(typeof(Game), nameof(Game.Start))]
static class TradeEffectsGameStartPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        TradeEffects.Register();
    }
}
