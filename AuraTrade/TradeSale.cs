using System.Collections.Generic;

namespace OttoAura.AuraTrade;

/// One stack, or the part of one, going into a sale.
internal readonly struct TradeLot
{
    internal readonly ItemDrop.ItemData Item;
    internal readonly int Amount;

    internal TradeLot(ItemDrop.ItemData item, int amount)
    {
        Item = item;
        Amount = amount;
    }
}

/// What a sale needs beyond the seller's inventory: a balance to credit, and somewhere to put
/// a valuable that no longer fits back in the pack. The game pays through OttoPay and drops at
/// the seller's feet; the tests keep a ledger instead.
internal interface ITradeDesk
{
    bool TryDeposit(int coins);

    void Drop(ItemDrop.ItemData item);
}

internal enum SaleOutcome
{
    Sold,
    Untradeable,
    LostTrack,
    DepositRefused,
}

internal readonly struct SaleResult
{
    internal readonly SaleOutcome Outcome;
    internal readonly TradeValuation.Quote Quote;
    internal readonly ItemDrop.ItemData? MostValuable;

    internal SaleResult(SaleOutcome outcome, TradeValuation.Quote quote, ItemDrop.ItemData? mostValuable = null)
    {
        Outcome = outcome;
        Quote = quote;
        MostValuable = mostValuable;
    }
}

/// Sells a set of lots as one sale with one fee.
internal static class TradeSale
{
    /// The items leave the inventory first and the net is deposited after. Removal is the step
    /// that can fail, and a deposit made before a failed removal would hand out coins for
    /// nothing. The deposit only fails for a non-member or an overflowing balance, and then the
    /// items go back.
    internal static SaleResult Complete(Inventory inventory, IReadOnlyList<TradeLot> lots, TradeFees fees, ITradeDesk desk)
    {
        long gross = 0;
        foreach (TradeLot lot in lots)
        {
            gross += TradeValuation.Worth(lot.Item, lot.Amount);
        }

        TradeValuation.Quote quote = TradeValuation.QuoteFor(gross, fees);
        if (!quote.Tradeable)
        {
            return new SaleResult(SaleOutcome.Untradeable, quote);
        }

        List<TradeLot> removed = new(lots.Count);
        foreach (TradeLot lot in lots)
        {
            bool wholeStack = lot.Amount == lot.Item.m_stack;
            if (!(wholeStack ? inventory.RemoveItem(lot.Item) : inventory.RemoveItem(lot.Item, lot.Amount)))
            {
                Restore(inventory, removed, desk);
                return new SaleResult(SaleOutcome.LostTrack, quote);
            }

            removed.Add(lot);
        }

        if (!desk.TryDeposit(quote.Net))
        {
            Restore(inventory, removed, desk);
            return new SaleResult(SaleOutcome.DepositRefused, quote);
        }

        return new SaleResult(SaleOutcome.Sold, quote, MostValuable(removed));
    }

    /// A split leaves the rest of its stack in the inventory, with m_stack already lowered, so
    /// the amount goes back onto it. A whole stack was taken out, and the room it left is still
    /// free for AddItem. The drop is only there so a valuable can never vanish unpaid.
    private static void Restore(Inventory inventory, List<TradeLot> removed, ITradeDesk desk)
    {
        foreach (TradeLot lot in removed)
        {
            if (inventory.ContainsItem(lot.Item))
            {
                lot.Item.m_stack += lot.Amount;
                inventory.Changed();
                continue;
            }

            if (!inventory.AddItem(lot.Item))
            {
                desk.Drop(lot.Item);
            }
        }
    }

    private static ItemDrop.ItemData MostValuable(List<TradeLot> lots)
    {
        TradeLot best = lots[0];
        foreach (TradeLot lot in lots)
        {
            if (TradeValuation.Worth(lot.Item, lot.Amount) > TradeValuation.Worth(best.Item, best.Amount))
            {
                best = lot;
            }
        }

        return best.Item;
    }
}
