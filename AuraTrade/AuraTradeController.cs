using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace OttoAura.AuraTrade;

// AuraTradeController: the Merchant Guild buys valuables. Two ways in:
//
//   Drop on the deposit arrow   pick a valuable, a stack or a split of one, up in the inventory and
//                               drop it on OttoPay's balance icon. OttoPay owns the icon and the
//                               drag; this class answers through the deposit handler API.
//   AltPlace + Use on a ward    sells every valuable in the inventory at once.
//
// Either way a sale needs AuraPay on and the player standing inside a ward that is switched on
// and lists them as creator or permitted, the same ward the aura heals in. OttoPay asks canDeposit
// on every dragged frame, which is what shows or hides the arrow, so nothing is cached.
//
// The items leave the inventory first and the net is deposited after. Removal is the step that
// can fail, and a deposit made before a failed removal would hand out coins for nothing. The
// deposit only fails for a non-member or an overflowing balance; if it does, the items go back.
internal static class AuraTradeController
{
    // OttoPay titles the arrow's tooltip with the handler name.
    internal const string HandlerName = "Sell to the Merchant Guild";

    private readonly struct Lot
    {
        internal readonly ItemDrop.ItemData Item;
        internal readonly int Amount;

        internal Lot(ItemDrop.ItemData item, int amount)
        {
            Item = item;
            Amount = amount;
        }
    }

    private static readonly List<Lot> _lots = new();

    internal static void Register()
    {
        OttoPayBridge.RegisterDepositHandler(HandlerName, CanSell, Sell, Describe);
    }

    internal static void Unregister()
    {
        OttoPayBridge.UnregisterDepositHandler(HandlerName);
    }

    internal static bool IsAvailable(Player? player) =>
        player != null
        && OttoAuraPlugin.AuraTradeEnabled.Value == OttoAuraPlugin.Toggle.On
        && OttoPayBridge.CanDeposit
        && OttoPayBridge.IsAuraPayEnabled()
        && WardAura.FindAuraWard(player) != null;

    internal static bool IsTradingPost(PrivateArea? area) =>
        area != null
        && area.m_ownerFaction == Character.Faction.Players
        && area.IsEnabled()
        && area.HaveLocalAccess();

    // Only the player's own inventory sells. A valuable dragged out of an open chest does not.
    private static bool CanSell(Inventory inventory, ItemDrop.ItemData item, int amount)
    {
        Player? player = Player.m_localPlayer;
        return player != null
               && amount > 0
               && inventory == player.GetInventory()
               && TradeValuation.IsValuable(item)
               && IsAvailable(player);
    }

    private static string Describe(Inventory inventory, ItemDrop.ItemData item, int amount)
    {
        TradeValuation.Quote quote = TradeValuation.QuoteFor(TradeValuation.Worth(item, amount));
        string what = WhatFor(item, amount);
        if (!quote.Tradeable)
        {
            return RefusalFor(what, quote);
        }

        return $"Click to sell {what}.\n\n"
               + $"Worth: {quote.Gross} coins\n"
               + $"AuraPay fee: {quote.Fee} coins\n"
               + $"To your balance: {quote.Net} coins\n\n"
               + $"The fee is {OttoAuraPlugin.AuraTradeFlatFee.Value} coins plus {OttoAuraPlugin.AuraTradePercentFee.Value:0.##}% of each sale, so fewer, larger sales keep more.";
    }

    // The deposit arrow's sale. Answers true only when the items are gone and the net is in the
    // balance; on false OttoPay leaves the drag as it was.
    private static bool Sell(Inventory inventory, ItemDrop.ItemData item, int amount)
    {
        Player? player = Player.m_localPlayer;
        if (player == null || !CanSell(inventory, item, amount) || amount > item.m_stack || !inventory.ContainsItem(item))
        {
            return false;
        }

        PrivateArea? ward = WardAura.FindAuraWard(player);
        if (ward == null)
        {
            return false;
        }

        _lots.Clear();
        _lots.Add(new Lot(item, amount));
        return Complete(player, ward, WhatFor(item, amount));
    }

    // AltPlace + Use on the ward. Always answers true, so the press never falls through to vanilla
    // and switches the ward off.
    internal static bool SellAll(Player player, PrivateArea ward)
    {
        _lots.Clear();
        int count = 0;
        foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
        {
            if (TradeValuation.IsValuable(item))
            {
                _lots.Add(new Lot(item, item.m_stack));
                count += item.m_stack;
            }
        }

        if (count == 0)
        {
            Tell(player, "You carry nothing the Merchant Guild will buy.");
            return true;
        }

        Complete(player, ward, count == 1 ? "1 valuable" : $"{count} valuables");
        return true;
    }

    // Appended to the ward's hover text, after vanilla has already localized its own.
    internal static string HoverLine(PrivateArea ward)
    {
        Player? player = Player.m_localPlayer;
        if (!IsAvailable(player) || !IsTradingPost(ward))
        {
            return "";
        }

        long gross = 0;
        foreach (ItemDrop.ItemData item in player!.GetInventory().GetAllItems())
        {
            if (TradeValuation.IsValuable(item))
            {
                gross += TradeValuation.Worth(item, item.m_stack);
            }
        }

        if (gross == 0)
        {
            return "";
        }

        TradeValuation.Quote quote = TradeValuation.QuoteFor(gross);
        string key = "[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>]";
        string line = quote.Tradeable
            ? $"\n{key} Sell all valuables: {quote.Gross} worth, {quote.Fee} fee, {quote.Net} to your balance"
            : $"\n{key} Sell all valuables: the {quote.Fee} coin fee would take all {quote.Gross} they are worth";
        return Localization.instance.Localize(line);
    }

    // Sells everything in _lots as one sale with one fee.
    private static bool Complete(Player player, PrivateArea ward, string what)
    {
        long gross = 0;
        foreach (Lot lot in _lots)
        {
            gross += TradeValuation.Worth(lot.Item, lot.Amount);
        }

        TradeValuation.Quote quote = TradeValuation.QuoteFor(gross);
        if (!quote.Tradeable)
        {
            Tell(player, RefusalFor(what, quote));
            return false;
        }

        Inventory inventory = player.GetInventory();
        List<Lot> removed = new(_lots.Count);
        foreach (Lot lot in _lots)
        {
            bool wholeStack = lot.Amount == lot.Item.m_stack;
            if (!(wholeStack ? inventory.RemoveItem(lot.Item) : inventory.RemoveItem(lot.Item, lot.Amount)))
            {
                Restore(player, inventory, removed);
                Tell(player, "The Merchant Guild lost track of your valuables. Nothing was sold.");
                return false;
            }
            removed.Add(lot);
        }

        if (!OttoPayBridge.TryDeposit(quote.Net))
        {
            Restore(player, inventory, removed);
            Tell(player, "The Merchant Bank refused the deposit. Your valuables are back in your pack.");
            return false;
        }

        TradeEffects.Broadcast(player, ward, MostValuable(removed));
        Tell(player, $"The Merchant Guild bought {what} for {quote.Gross} coins. AuraPay kept {quote.Fee}, and {quote.Net} went to your balance.");
        return true;
    }

    // A split leaves the rest of its stack in the inventory, with m_stack already lowered, so the
    // amount goes back onto it. A whole stack was taken out, and the room it left is still free for
    // AddItem. The drop is only there so a valuable can never vanish unpaid.
    private static void Restore(Player player, Inventory inventory, List<Lot> removed)
    {
        foreach (Lot lot in removed)
        {
            if (inventory.ContainsItem(lot.Item))
            {
                lot.Item.m_stack += lot.Amount;
                inventory.Changed();
                continue;
            }

            if (!inventory.AddItem(lot.Item))
            {
                Transform transform = player.transform;
                ItemDrop.DropItem(lot.Item, lot.Item.m_stack, transform.position + transform.forward + Vector3.up, transform.rotation);
            }
        }
    }

    private static ItemDrop.ItemData MostValuable(List<Lot> lots)
    {
        Lot best = lots[0];
        foreach (Lot lot in lots)
        {
            if (TradeValuation.Worth(lot.Item, lot.Amount) > TradeValuation.Worth(best.Item, best.Amount))
            {
                best = lot;
            }
        }
        return best.Item;
    }

    private static string WhatFor(ItemDrop.ItemData item, int amount)
    {
        string name = Localization.instance.Localize(item.m_shared.m_name);
        return amount > 1 ? $"{amount} {name}" : name;
    }

    private static string RefusalFor(string what, TradeValuation.Quote quote) =>
        $"{what} is worth {quote.Gross} coins, and the AuraPay fee of {quote.Fee} would take all of it. Sell more at once.";

    // Every message answers a deliberate drop or press, so there is nothing to throttle.
    private static void Tell(Player player, string message) =>
        player.Message(MessageHud.MessageType.TopLeft, message);
}

// AltPlace + Use on a ward. Vanilla ignores alt here, so plain Use still switches the ward on and
// off. The modified press belongs to AuraTrade whenever a sale is possible at this ward, and is
// never allowed to fall through and switch the ward off by accident.
[HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.Interact))]
static class PrivateAreaInteractPatch
{
    static bool Prefix(PrivateArea __instance, Humanoid human, bool hold, bool alt, ref bool __result)
    {
        if (hold
            || !alt
            || human != Player.m_localPlayer
            || !AuraTradeController.IsAvailable(Player.m_localPlayer)
            || !AuraTradeController.IsTradingPost(__instance))
        {
            return true;
        }

        __result = AuraTradeController.SellAll((Player)human, __instance);
        return false;
    }
}
