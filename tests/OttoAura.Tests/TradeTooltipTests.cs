using OttoAura.AuraTrade;

namespace OttoAura.Tests;

public class TradeTooltipTests
{
    private static readonly TradeFees Fees = new(5, 5f);

    [Fact]
    public void Puts_the_quote_right_under_the_value_line()
    {
        ItemDrop.ItemData ruby = Items.Valuable("$item_ruby", 20, 1);
        string tooltip = "A ruby\n$item_value: 20\n$item_weight: 0.1";

        string result = TradeTooltip.Insert(tooltip, ruby, 1, Fees);

        string[] lines = result.Split('\n');
        Assert.Equal("$item_value: 20", lines[1]);
        Assert.StartsWith("AuraTrade: ", lines[2]);
        Assert.Equal("$item_weight: 0.1", lines[3]);
    }

    [Fact]
    public void Quotes_one_item_and_the_whole_stack()
    {
        ItemDrop.ItemData ruby = Items.Valuable("$item_ruby", 20, 10);

        string result = TradeTooltip.Insert("A ruby\n$item_value: 20", ruby, 10, Fees);

        Assert.Contains("AuraTrade each: <color=orange>20</color> worth, <color=orange>6</color> fee, <color=orange>14</color> net", result);
        Assert.Contains("AuraTrade stack of 10: <color=orange>200</color> worth, <color=orange>15</color> fee, <color=orange>185</color> net", result);
    }

    [Fact]
    public void Appends_at_the_end_when_there_is_no_value_line()
    {
        ItemDrop.ItemData ruby = Items.Valuable("$item_ruby", 20, 1);

        string result = TradeTooltip.Insert("A ruby", ruby, 1, Fees);

        Assert.StartsWith("A ruby\nAuraTrade: ", result);
    }

    [Fact]
    public void Says_so_when_the_fee_takes_it_all()
    {
        ItemDrop.ItemData amber = Items.Valuable("$item_amber", 5, 1);

        string result = TradeTooltip.Insert("Amber", amber, 1, Fees);

        Assert.Contains("the <color=orange>5</color> fee takes it all", result);
    }
}
