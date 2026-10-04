using OttoAura.WispFog;
using UnityEngine;
using static OttoAura.WispFog.FogBubbleMath;

namespace OttoAura.Tests;

/// FogKind is internal, so fog modes are passed by name.
public class FogBubbleMathTests
{
    private static FogKind Kind(string name) => Enum.Parse<FogKind>(name);

    private static readonly Vector3 Forward = new(0f, 0f, 1f);

    [Theory]
    // camera at the center: the whole radius ahead is inside
    [InlineData(0f, 100f, 10f, 10f)]
    // surface closer than the bubble edge: only up to the surface counts
    [InlineData(0f, 4f, 10f, 4f)]
    // bubble straight ahead, surface beyond it: the full diameter counts
    [InlineData(-50f, 100f, 10f, 20f)]
    // bubble straight ahead, surface inside it: from the near edge to the surface
    [InlineData(-50f, 45f, 10f, 5f)]
    // bubble behind the camera
    [InlineData(50f, 100f, 10f, 0f)]
    // surface in front of the bubble
    [InlineData(-50f, 30f, 10f, 0f)]
    public void Length_inside_a_bubble_on_the_view_axis(float cameraZ, float maxDistance, float radius, float expected)
    {
        Vector3 camera = new(0f, 0f, cameraZ);
        Assert.Equal(expected, LengthInside(camera, Forward, maxDistance, Vector3.zero, radius), 3);
    }

    [Fact]
    public void A_ray_that_misses_the_bubble_has_nothing_inside()
    {
        Vector3 camera = new(20f, 0f, -50f);
        Assert.Equal(0f, LengthInside(camera, Forward, 100f, Vector3.zero, 10f));
    }

    [Fact]
    public void An_off_center_ray_crosses_a_chord()
    {
        // 6 off the axis of a radius-10 sphere: the chord is 2 * sqrt(100 - 36) = 16.
        Vector3 camera = new(6f, 0f, -50f);
        Assert.Equal(16f, LengthInside(camera, Forward, 100f, Vector3.zero, 10f), 3);
    }

    [Theory]
    [InlineData("Linear")]
    [InlineData("Exponential")]
    [InlineData("ExponentialSquared")]
    public void A_ray_wholly_inside_a_bubble_keeps_no_fog(string kind)
    {
        Assert.Equal(0f, FogKept(Kind(kind), 0.01f, 0f, 300f, 80f, 1f), 4);
    }

    [Theory]
    [InlineData("Linear")]
    [InlineData("Exponential")]
    [InlineData("ExponentialSquared")]
    public void A_ray_outside_every_bubble_keeps_all_its_fog(string kind)
    {
        Assert.Equal(1f, FogKept(Kind(kind), 0.01f, 0f, 300f, 80f, 0f), 4);
    }

    [Fact]
    public void Fog_kept_falls_as_more_of_the_ray_is_inside()
    {
        float previous = 1f;
        for (float fraction = 0.1f; fraction <= 1f; fraction += 0.1f)
        {
            float kept = FogKept(FogKind.ExponentialSquared, 0.01f, 0f, 0f, 120f, fraction);
            Assert.True(kept < previous, $"kept {kept} at {fraction} did not fall below {previous}");
            previous = kept;
        }
    }

    [Fact]
    public void Exponential_squared_matches_the_game_formula()
    {
        // Post-processing Stack v1: exp2(-(density * z)^2).
        float density = 0.01f;
        float z = 150f;
        float expected = Mathf.Pow(2f, -(density * z) * (density * z));
        Assert.Equal(expected, Transmittance(FogKind.ExponentialSquared, density, 0f, 0f, z), 5);
    }

    [Fact]
    public void A_pixel_with_no_fog_is_left_alone()
    {
        Assert.Equal(1f, FogKept(FogKind.ExponentialSquared, 0f, 0f, 0f, 50f, 0.5f));
    }
}
