using OttoAura.AuraTrade;

namespace OttoAura.Tests;

/// Runs the sale against the game's real Inventory class.
public class TradeSaleTests
{
    private static readonly TradeFees Fees = new(5, 5f);

    [Fact]
    public void Sells_whole_stacks_as_one_sale_with_one_fee()
    {
        Inventory pack = Items.Pack(4, 1);
        ItemDrop.ItemData rubies = Items.Valuable("$item_ruby", 20, 5);
        ItemDrop.ItemData pearls = Items.Valuable("$item_amberpearl", 10, 4);
        pack.AddItem(rubies);
        pack.AddItem(pearls);
        FakeDesk desk = new();

        SaleResult sale = TradeSale.Complete(pack, new[] { new TradeLot(rubies, 5), new TradeLot(pearls, 4) }, Fees, desk);

        Assert.Equal(SaleOutcome.Sold, sale.Outcome);
        Assert.Equal(140, sale.Quote.Gross);
        Assert.Equal(new[] { sale.Quote.Net }, desk.Deposits);
        Assert.Same(rubies, sale.MostValuable);
        Assert.Equal(0, pack.CountItems("$item_ruby"));
        Assert.Equal(0, pack.CountItems("$item_amberpearl"));
    }

    [Fact]
    public void Sells_part_of_a_stack_and_leaves_the_rest()
    {
        Inventory pack = Items.Pack(2, 1);
        ItemDrop.ItemData rubies = Items.Valuable("$item_ruby", 20, 10);
        pack.AddItem(rubies);
        FakeDesk desk = new();

        SaleResult sale = TradeSale.Complete(pack, new[] { new TradeLot(rubies, 4) }, Fees, desk);

        Assert.Equal(SaleOutcome.Sold, sale.Outcome);
        Assert.Equal(80, sale.Quote.Gross);
        Assert.Equal(6, pack.CountItems("$item_ruby"));
    }

    [Fact]
    public void A_sale_the_fee_would_swallow_removes_nothing_and_deposits_nothing()
    {
        Inventory pack = Items.Pack(2, 1);
        ItemDrop.ItemData amber = Items.Valuable("$item_amber", 5, 1);
        pack.AddItem(amber);
        FakeDesk desk = new();

        SaleResult sale = TradeSale.Complete(pack, new[] { new TradeLot(amber, 1) }, Fees, desk);

        Assert.Equal(SaleOutcome.Untradeable, sale.Outcome);
        Assert.Empty(desk.Deposits);
        Assert.Equal(1, pack.CountItems("$item_amber"));
    }

    [Fact]
    public void A_refused_deposit_puts_whole_stacks_back()
    {
        Inventory pack = Items.Pack(4, 1);
        ItemDrop.ItemData rubies = Items.Valuable("$item_ruby", 20, 5);
        ItemDrop.ItemData pearls = Items.Valuable("$item_amberpearl", 10, 4);
        pack.AddItem(rubies);
        pack.AddItem(pearls);
        FakeDesk desk = new() { AcceptsDeposits = false };

        SaleResult sale = TradeSale.Complete(pack, new[] { new TradeLot(rubies, 5), new TradeLot(pearls, 4) }, Fees, desk);

        Assert.Equal(SaleOutcome.DepositRefused, sale.Outcome);
        Assert.Equal(5, pack.CountItems("$item_ruby"));
        Assert.Equal(4, pack.CountItems("$item_amberpearl"));
        Assert.Empty(desk.Dropped);
    }

    [Fact]
    public void A_refused_deposit_puts_a_split_back_on_its_stack()
    {
        Inventory pack = Items.Pack(2, 1);
        ItemDrop.ItemData rubies = Items.Valuable("$item_ruby", 20, 10);
        pack.AddItem(rubies);
        FakeDesk desk = new() { AcceptsDeposits = false };

        SaleResult sale = TradeSale.Complete(pack, new[] { new TradeLot(rubies, 4) }, Fees, desk);

        Assert.Equal(SaleOutcome.DepositRefused, sale.Outcome);
        Assert.Equal(10, pack.CountItems("$item_ruby"));
        Assert.Single(pack.GetAllItems());
    }

    [Fact]
    public void A_lot_that_left_the_pack_undoes_the_lots_already_taken_and_deposits_nothing()
    {
        Inventory pack = Items.Pack(4, 1);
        ItemDrop.ItemData rubies = Items.Valuable("$item_ruby", 20, 10);
        pack.AddItem(rubies);
        // Not in the pack: a split of it cannot be removed.
        ItemDrop.ItemData elsewhere = Items.Valuable("$item_amberpearl", 10, 8);
        FakeDesk desk = new();

        SaleResult sale = TradeSale.Complete(pack, new[] { new TradeLot(rubies, 4), new TradeLot(elsewhere, 3) }, Fees, desk);

        Assert.Equal(SaleOutcome.LostTrack, sale.Outcome);
        Assert.Empty(desk.Deposits);
        Assert.Equal(10, pack.CountItems("$item_ruby"));
        Assert.Equal(8, elsewhere.m_stack);
    }
}
