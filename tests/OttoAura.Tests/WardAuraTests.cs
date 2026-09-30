namespace OttoAura.Tests;

public class WardAuraTests
{
    [Fact]
    public void Collects_worn_gear_that_can_be_repaired()
    {
        Inventory pack = Items.Pack(4, 1);
        ItemDrop.ItemData sword = Items.Gear("$item_sword", 40f);
        ItemDrop.ItemData torch = Items.Gear("$item_torch", 10f, canBeRepaired: false);
        ItemDrop.ItemData helmet = Items.Gear("$item_helmet", 100f);
        pack.AddItem(sword);
        pack.AddItem(torch);
        pack.AddItem(helmet);
        List<ItemDrop.ItemData> worn = new() { helmet };

        WardAura.CollectRepairable(pack, worn);

        Assert.Equal(new[] { sword }, worn);
    }

    [Fact]
    public void Repairs_a_share_of_maximum_durability_and_lists_what_reached_full()
    {
        ItemDrop.ItemData sword = Items.Gear("$item_sword", 40f, maxDurability: 200f);
        ItemDrop.ItemData shield = Items.Gear("$item_shield", 97f);
        List<ItemDrop.ItemData> repaired = new();

        WardAura.RepairStep(new List<ItemDrop.ItemData> { sword, shield }, 5f, repaired);

        Assert.Equal(50f, sword.m_durability);
        Assert.Equal(100f, shield.m_durability);
        Assert.Equal(new[] { shield }, repaired);
    }

    [Theory]
    [InlineData(5f, 1f, 1, "heals you and repairs your gear within 30 m, paid through AuraPay")]
    [InlineData(5f, 1f, 0, "heals you and repairs your gear within 30 m<")]
    [InlineData(5f, 0f, 1, "repairs your gear within 30 m, paid through AuraPay")]
    // no repair, nothing to pay for
    [InlineData(0f, 1f, 1, "heals you within 30 m<")]
    [InlineData(0f, 0f, 0, "is idle within 30 m<")]
    public void Hover_line_says_what_the_aura_does(float repairPercent, float healPerSecond, int coinsPerItem, string expected)
    {
        string line = WardAura.HoverLine(repairPercent, healPerSecond, coinsPerItem, 30f);

        Assert.Contains("Aura " + expected, line);
    }
}
