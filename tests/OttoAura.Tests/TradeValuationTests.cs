using OttoAura.AuraTrade;

namespace OttoAura.Tests;

public class TradeValuationTests
{
    private static readonly TradeFees DefaultFees = new(5, 5f);

    [Theory]
    // flat 5 plus 5% rounded up: 100 -> 5 + 5
    [InlineData(100, 10, 90)]
    // 5% of 101 is 5.05, rounded up to 6
    [InlineData(101, 11, 90)]
    // the fee never exceeds the gross, and a trade that nets nothing is not tradeable
    [InlineData(5, 5, 0)]
    [InlineData(1, 1, 0)]
    public void Quotes_a_flat_fee_plus_a_percent_rounded_up(long gross, int fee, int net)
    {
        TradeValuation.Quote quote = TradeValuation.QuoteFor(gross, DefaultFees);

        Assert.Equal((int)gross, quote.Gross);
        Assert.Equal(fee, quote.Fee);
        Assert.Equal(net, quote.Net);
        Assert.Equal(net > 0, quote.Tradeable);
    }

    [Fact]
    public void A_fractional_percent_does_not_round_up_twice()
    {
        // 2.9% of 1000 is exactly 29, but 2.9f * 1000 in floats is a hair above it.
        TradeValuation.Quote quote = TradeValuation.QuoteFor(1000, new TradeFees(0, 2.9f));

        Assert.Equal(29, quote.Fee);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData((long)int.MaxValue + 1)]
    public void A_gross_out_of_range_quotes_as_nothing(long gross)
    {
        TradeValuation.Quote quote = TradeValuation.QuoteFor(gross, DefaultFees);

        Assert.Equal(0, quote.Gross);
        Assert.False(quote.Tradeable);
    }

    [Fact]
    public void Negative_fees_count_as_zero()
    {
        TradeValuation.Quote quote = TradeValuation.QuoteFor(100, new TradeFees(-5, -5f));

        Assert.Equal(0, quote.Fee);
        Assert.Equal(100, quote.Net);
    }

    [Fact]
    public void One_big_trade_keeps_more_than_the_same_valuables_sold_one_at_a_time()
    {
        int together = TradeValuation.QuoteFor(10 * 30, DefaultFees).Net;
        int apart = 10 * TradeValuation.QuoteFor(30, DefaultFees).Net;

        Assert.True(together > apart);
    }

    [Fact]
    public void Worth_does_not_overflow_an_int()
    {
        ItemDrop.ItemData item = Items.Valuable("$item_ruby", int.MaxValue, 2);

        Assert.Equal(2L * int.MaxValue, TradeValuation.Worth(item, 2));
    }

    [Fact]
    public void Coins_are_never_valuable()
    {
        Assert.False(TradeValuation.IsValuable(Items.Valuable("$item_coins", 1, 10), ""));
    }

    [Fact]
    public void An_item_without_value_is_not_valuable()
    {
        Assert.False(TradeValuation.IsValuable(Items.Valuable("$item_wood", 0, 10), ""));
        Assert.False(TradeValuation.IsValuable(null, ""));
    }

    [Fact]
    public void An_item_with_value_is_valuable()
    {
        Assert.True(TradeValuation.IsValuable(Items.Valuable("$item_ruby", 20, 1), "AmberPearl"));
    }
}
