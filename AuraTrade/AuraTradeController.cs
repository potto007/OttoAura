using UnityEngine;

namespace OttoAura.AuraTrade;

// AuraTradeController: the Merchant Guild buys valuables through OttoPay's balance icon. The player
// picks a valuable, a stack or a split of one, up in their inventory and drops it on the deposit
// arrow. OttoPay owns the icon and the drag; this class answers its three questions through the
// deposit handler API: may this drag be sold, what would it pay, and sell it.
//
// A sale needs AuraPay on and the player standing inside a ward that is switched on and lists
// them as creator or permitted, the same ward the aura heals in. OttoPay asks canDeposit on every
// dragged frame, which is what shows or hides the arrow, so the conditions are never cached.
//
// The items leave the inventory first and the net is deposited after. Removal is the step that
// can fail, and a deposit made before a failed removal would hand out coins for nothing. The
// deposit only fails for a non-member or an overflowing balance; if it does, the items go back.
internal static class AuraTradeController
{
    // OttoPay titles the arrow's tooltip with the handler name.
    internal const string HandlerName = "Sell to the Merchant Guild";

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

    // Answers true only when the items are gone and the net is in the balance. On false OttoPay
    // leaves the drag as it was.
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

        TradeValuation.Quote quote = TradeValuation.QuoteFor(TradeValuation.Worth(item, amount));
        string what = WhatFor(item, amount);
        if (!quote.Tradeable)
        {
            Tell(player, RefusalFor(what, quote));
            return false;
        }

        bool wholeStack = amount == item.m_stack;
        bool removed = wholeStack ? inventory.RemoveItem(item) : inventory.RemoveItem(item, amount);
        if (!removed)
        {
            Tell(player, "The Merchant Guild lost track of your valuables. Nothing was sold.");
            return false;
        }

        if (!OttoPayBridge.TryDeposit(quote.Net))
        {
            Restore(player, inventory, item, amount, wholeStack);
            Tell(player, "The Merchant Bank refused the deposit. Your valuables are back in your pack.");
            return false;
        }

        TradeEffects.Broadcast(player, ward, item);
        Tell(player, $"The Merchant Guild bought {what} for {quote.Gross} coins. AuraPay kept {quote.Fee}, and {quote.Net} went to your balance.");
        return true;
    }

    // A split leaves the rest of the stack in place, so the amount simply goes back onto it. A
    // whole stack was taken out, and the room it left is still free for AddItem. The drop is only
    // there so a valuable can never vanish unpaid.
    private static void Restore(Player player, Inventory inventory, ItemDrop.ItemData item, int amount, bool wholeStack)
    {
        if (!wholeStack)
        {
            item.m_stack += amount;
            inventory.Changed();
            return;
        }

        if (!inventory.AddItem(item))
        {
            Transform transform = player.transform;
            ItemDrop.DropItem(item, item.m_stack, transform.position + transform.forward + Vector3.up, transform.rotation);
        }
    }

    private static string WhatFor(ItemDrop.ItemData item, int amount)
    {
        string name = Localization.instance.Localize(item.m_shared.m_name);
        return amount > 1 ? $"{amount} {name}" : name;
    }

    private static string RefusalFor(string what, TradeValuation.Quote quote) =>
        $"{what} is worth {quote.Gross} coins, and the AuraPay fee of {quote.Fee} would take all of it. Sell more at once.";

    // Every message answers a deliberate drop, so there is nothing to throttle.
    private static void Tell(Player player, string message) =>
        player.Message(MessageHud.MessageType.TopLeft, message);
}
