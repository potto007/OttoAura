using System.Globalization;

namespace OttoAura.Tests;

public class DurabilityHoldTests
{
    private static readonly DateTime Now = new(2026, 10, 5, 14, 0, 0);

    [Theory]
    [InlineData(0f, 0)]
    [InlineData(0.49f, 0)]
    [InlineData(0.5f, 5)]
    [InlineData(1f, 10)]
    public void Hold_lasts_ten_minutes_times_the_skill_factor_from_half_skill(float skillFactor, int minutes)
    {
        Assert.Equal(minutes, DurabilityHold.MinutesFor(skillFactor));
    }

    [Fact]
    public void Holds_until_the_deadline()
    {
        ItemDrop.ItemData sword = Items.Gear("$item_sword", 100f);
        DurabilityHold.Start(sword, 1f, Now);

        DurabilityHold.State state = DurabilityHold.Refresh(sword, Now.AddMinutes(9));

        Assert.Equal(DurabilityHold.State.Holding, state);
        Assert.False(sword.m_shared.m_useDurability);
    }

    [Fact]
    public void Gives_durability_back_and_clears_the_keys_past_the_deadline()
    {
        ItemDrop.ItemData sword = Items.Gear("$item_sword", 100f);
        DurabilityHold.Start(sword, 1f, Now);
        DurabilityHold.Refresh(sword, Now.AddMinutes(1));

        DurabilityHold.State state = DurabilityHold.Refresh(sword, Now.AddMinutes(11));

        Assert.Equal(DurabilityHold.State.Expired, state);
        Assert.True(sword.m_shared.m_useDurability);
        Assert.Empty(sword.m_customData);
        Assert.Equal(DurabilityHold.State.None, DurabilityHold.Refresh(sword, Now.AddMinutes(12)));
    }

    [Fact]
    public void Leaves_gear_without_a_hold_alone()
    {
        ItemDrop.ItemData sword = Items.Gear("$item_sword", 100f);

        Assert.Equal(DurabilityHold.State.None, DurabilityHold.Refresh(sword, Now));
        Assert.True(sword.m_shared.m_useDurability);
        Assert.Equal(DurabilityHold.State.None, DurabilityHold.Refresh(null, Now));
    }

    [Fact]
    public void A_garbled_durability_value_does_not_throw()
    {
        ItemDrop.ItemData sword = Items.Gear("$item_sword", 100f);
        DurabilityHold.Start(sword, 1f, Now);
        sword.m_customData[DurabilityHold.UseDurabilityKey] = "maybe";

        Assert.Equal(DurabilityHold.State.Expired, DurabilityHold.Refresh(sword, Now.AddHours(1)));
        Assert.Empty(sword.m_customData);
    }

    /// The deadline is written in the invariant culture, month first. A day first culture
    /// read it back as 10 May, long past, and a 29th as no date at all.
    [Theory]
    [InlineData("en-GB")]
    [InlineData("de-DE")]
    public void Holds_in_a_day_first_culture(string culture)
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(culture);
        try
        {
            ItemDrop.ItemData sword = Items.Gear("$item_sword", 100f);
            DurabilityHold.Start(sword, 1f, Now);
            Assert.Equal(DurabilityHold.State.Holding, DurabilityHold.Refresh(sword, Now.AddMinutes(9)));

            ItemDrop.ItemData shield = Items.Gear("$item_shield", 100f);
            DateTime late = new(2026, 9, 29, 14, 0, 0);
            DurabilityHold.Start(shield, 1f, late);
            Assert.Equal(DurabilityHold.State.Holding, DurabilityHold.Refresh(shield, late.AddMinutes(9)));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
