using OttoAura.AuraBoost;
using UnityEngine;

namespace OttoAura.Tests;

/// Footing is internal, so the expected footing is passed by name.
public class AuraBoostTests
{
    [Theory]
    // no other discount: AuraBoost's fraction applies
    [InlineData(10f, 10f, 0.5f, 5f)]
    [InlineData(10f, 10f, 0f, 0f)]
    // another mod already halves the drain: the bigger discount wins, they do not multiply
    [InlineData(10f, 5f, 0.8f, 5f)]
    [InlineData(10f, 5f, 0.2f, 2f)]
    // another mod charges extra: AuraBoost applies on top of it
    [InlineData(10f, 20f, 0.5f, 10f)]
    // nothing to discount
    [InlineData(0f, 3f, 0.5f, 3f)]
    public void Bigger_discount_wins(float gameDrain, float drain, float usage, float expected)
    {
        Assert.Equal(expected, AuraBoostEffect.DiscountedDrain(gameDrain, drain, usage), 4);
    }

    [Theory]
    [InlineData(0f, 0f, 1f, "Road")]
    [InlineData(1f, 0f, 0f, "Trail")]
    [InlineData(0f, 1f, 0f, "Wild")]
    // deep snow paints every channel, so none leads
    [InlineData(1f, 1f, 1f, "Wild")]
    [InlineData(0.2f, 0f, 0.5f, "Wild")]
    public void Terrain_paint_counts_only_a_clearly_leading_channel(float r, float g, float b, string expected)
    {
        Assert.Equal(expected, Ground.FromPaint(new Color(r, g, b, 1f)).ToString());
    }

    [Theory]
    [InlineData(WearNTear.MaterialType.Stone, "Road")]
    [InlineData(WearNTear.MaterialType.Marble, "Road")]
    [InlineData(WearNTear.MaterialType.Wood, "Trail")]
    [InlineData(WearNTear.MaterialType.Iron, "Trail")]
    public void Built_floors_count_by_material(WearNTear.MaterialType material, string expected)
    {
        Assert.Equal(expected, Ground.FromPiece(material).ToString());
    }
}
