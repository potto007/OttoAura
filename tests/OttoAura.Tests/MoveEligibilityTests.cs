using OttoAura.AuraMove;

namespace OttoAura.Tests;

public class MoveEligibilityTests
{
    [Theory]
    [InlineData("fire_pit,bonfire,hearth", "bonfire", true)]
    [InlineData("fire_pit, bonfire ,hearth", "bonfire", true)]
    [InlineData("fire_pit,bonfire", "bonfire_large", false)]
    [InlineData("", "bonfire", false)]
    public void Matches_whole_trimmed_prefab_names(string csv, string prefab, bool expected)
    {
        Assert.Equal(expected, MoveEligibility.IsListed(csv, prefab));
    }
}
