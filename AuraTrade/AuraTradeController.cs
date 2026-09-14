using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace OttoAura.AuraTrade;

// AuraTradeController: the Merchant Guild buys valuables at a ward. Two ways in, both on a player
// ward that is on and lists the player as creator or permitted, the same access the aura asks for:
//
//   Use a valuable from the hotbar on the ward   sells that one stack.
//   AltPlace + Use on the ward                   sells every valuable in the inventory at once.
//
// The items leave the inventory first and the net is deposited after. Removal is the step that
// can fail, and a deposit made before a failed removal would hand out coins for nothing. The
// deposit only fails for a non-member or an overflowing balance; if it does, the items go back.
//
// Everything is local player state, like the aura: the inventory and the bank balance both belong
// to the player running this client, so no RPC is needed.
internal static class AuraTradeController
{
    private static readonly List<ItemDrop.ItemData> _valuables = new();
    private static readonly List<ItemDrop.ItemData> _batch = new();

    // Re-read on every use: the server can switch AuraTrade off and the player can switch AuraPay
    // off at any moment. CanDeposit is false until OttoPay 1.6.0, which keeps AuraTrade dormant.
    internal static bool IsAvailable =>
        OttoAuraPlugin.AuraTradeEnabled.Value == OttoAuraPlugin.Toggle.On
        && OttoPayBridge.CanDeposit
        && OttoPayBridge.IsAuraPayEnabled();

    internal static bool IsTradingPost(PrivateArea? area) =>
        area != null
        && area.m_ownerFaction == Character.Faction.Players
        && area.IsEnabled()
        && area.HaveLocalAccess();

    internal static bool TradeStack(Player player, PrivateArea ward, ItemDrop.ItemData item)
    {
        if (!player.GetInventory().ContainsItem(item))
        {
            return false;
        }

        string name = Localization.instance.Localize(item.m_shared.m_name);
        string what = item.m_stack > 1 ? $"{item.m_stack} {name}" : name;
        TradeValuation.Quote quote = TradeValuation.QuoteFor(TradeValuation.Worth(item, item.m_stack));

        _batch.Clear();
        _batch.Add(item);
        Commit(player, ward, _batch, quote, what);
        return true;
    }

    internal static bool TradeAll(Player player, PrivateArea ward)
    {
        CollectValuables(player);
        if (_valuables.Count == 0)
        {
            Tell(player, "You carry nothing the Merchant Guild will buy.");
            return true;
        }

        int count = 0;
        foreach (ItemDrop.ItemData item in _valuables)
        {
            count += item.m_stack;
        }

        string what = count == 1 ? "1 valuable" : $"{count} valuables";
        TradeValuation.Quote quote = TradeValuation.QuoteFor(TotalWorth(_valuables));

        _batch.Clear();
        _batch.AddRange(_valuables);
        Commit(player, ward, _batch, quote, what);
        return true;
    }

    // Appended to the ward's hover text, after vanilla has already localized its own.
    internal static string HoverLine()
    {
        Player? player = Player.m_localPlayer;
        if (player == null || !IsAvailable)
        {
            return "";
        }

        CollectValuables(player);
        if (_valuables.Count == 0)
        {
            return "";
        }

        TradeValuation.Quote quote = TradeValuation.QuoteFor(TotalWorth(_valuables));
        string key = "[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>]";
        string line = quote.Tradeable
            ? $"\n{key} Sell all valuables: {quote.Gross} worth, {quote.Fee} fee, {quote.Net} to your balance"
            : $"\n{key} Sell all valuables: the {quote.Fee} coin fee would take all {quote.Gross} they are worth";
        return Localization.instance.Localize(line);
    }

    private static void Commit(Player player, PrivateArea ward, List<ItemDrop.ItemData> items, TradeValuation.Quote quote, string what)
    {
        if (!quote.Tradeable)
        {
            Tell(player, $"{what} is worth {quote.Gross} coins, and the AuraPay fee of {quote.Fee} would take all of it. Sell more at once.");
            return;
        }

        Inventory inventory = player.GetInventory();
        List<ItemDrop.ItemData> removed = new(items.Count);
        foreach (ItemDrop.ItemData item in items)
        {
            if (!inventory.RemoveItem(item))
            {
                Restore(player, removed);
                Tell(player, "The Merchant Guild lost track of your valuables. Nothing was sold.");
                return;
            }
            removed.Add(item);
        }

        if (!OttoPayBridge.TryDeposit(quote.Net))
        {
            Restore(player, removed);
            Tell(player, "The Merchant Bank refused the deposit. Your valuables are back in your pack.");
            return;
        }

        TradeEffects.Play(player, ward, MostValuable(removed));
        Tell(player, $"The Merchant Guild bought {what} for {quote.Gross} coins. AuraPay kept {quote.Fee}, and {quote.Net} went to your balance.");
    }

    // The removal just freed the room these came from, so AddItem finds a place for them. The
    // drop is only there so a valuable can never vanish unpaid.
    private static void Restore(Player player, List<ItemDrop.ItemData> items)
    {
        Inventory inventory = player.GetInventory();
        Transform transform = player.transform;
        foreach (ItemDrop.ItemData item in items)
        {
            if (!inventory.AddItem(item))
            {
                ItemDrop.DropItem(item, item.m_stack, transform.position + transform.forward + Vector3.up, transform.rotation);
            }
        }
    }

    private static void CollectValuables(Player player)
    {
        _valuables.Clear();
        foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
        {
            if (TradeValuation.IsValuable(item))
            {
                _valuables.Add(item);
            }
        }
    }

    private static long TotalWorth(List<ItemDrop.ItemData> items)
    {
        long total = 0;
        foreach (ItemDrop.ItemData item in items)
        {
            total += TradeValuation.Worth(item, item.m_stack);
        }
        return total;
    }

    private static ItemDrop.ItemData MostValuable(List<ItemDrop.ItemData> items)
    {
        ItemDrop.ItemData best = items[0];
        foreach (ItemDrop.ItemData item in items)
        {
            if (TradeValuation.Worth(item, item.m_stack) > TradeValuation.Worth(best, best.m_stack))
            {
                best = item;
            }
        }
        return best;
    }

    // Answer every deliberate press. Nothing here repeats on a tick, so there is nothing to throttle.
    private static void Tell(Player player, string message) =>
        player.Message(MessageHud.MessageType.TopLeft, message);
}

// A valuable used from the hotbar while hovering a ward. Vanilla wards accept no items and return
// false, which is what makes Humanoid.UseItem show "can't use"; a sale answers true instead.
[HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.UseItem))]
static class PrivateAreaUseItemPatch
{
    static void Postfix(PrivateArea __instance, Humanoid user, ItemDrop.ItemData item, ref bool __result)
    {
        if (__result
            || user != Player.m_localPlayer
            || !TradeValuation.IsValuable(item)
            || !AuraTradeController.IsAvailable
            || !AuraTradeController.IsTradingPost(__instance))
        {
            return;
        }

        __result = AuraTradeController.TradeStack((Player)user, __instance, item);
    }
}

// AltPlace + Use on a ward. Vanilla ignores alt here, so plain Use still switches the ward on and
// off; the modified press belongs to AuraTrade whenever the ward can trade, and is never allowed
// to fall through and switch the ward off by accident.
[HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.Interact))]
static class PrivateAreaInteractPatch
{
    static bool Prefix(PrivateArea __instance, Humanoid human, bool hold, bool alt, ref bool __result)
    {
        if (hold
            || !alt
            || human != Player.m_localPlayer
            || !AuraTradeController.IsAvailable
            || !AuraTradeController.IsTradingPost(__instance))
        {
            return true;
        }

        __result = AuraTradeController.TradeAll((Player)human, __instance);
        return false;
    }
}
