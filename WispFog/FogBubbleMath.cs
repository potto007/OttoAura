using UnityEngine;

namespace OttoAura.WispFog
{
    /// <summary>
    /// The arithmetic behind the fog bubble, kept free of Unity's native side so the unit
    /// tests can run it. The shader in unity/Assets/OttoAura/Shaders/FogBubble.shader repeats
    /// these formulas per pixel; change both together.
    /// </summary>
    internal static class FogBubbleMath
    {
        internal enum FogKind
        {
            Linear,
            Exponential,
            ExponentialSquared,
        }

        /// <summary>
        /// How much of the segment from <paramref name="origin"/> to
        /// <paramref name="maxDistance"/> along <paramref name="direction"/> lies inside the
        /// sphere. <paramref name="direction"/> must be normalized.
        /// </summary>
        internal static float LengthInside(Vector3 origin, Vector3 direction, float maxDistance, Vector3 center, float radius)
        {
            if (radius <= 0f || maxDistance <= 0f)
            {
                return 0f;
            }

            Vector3 offset = origin - center;
            float b = Vector3.Dot(offset, direction);
            float c = Vector3.Dot(offset, offset) - radius * radius;
            float discriminant = b * b - c;
            if (discriminant <= 0f)
            {
                return 0f;
            }

            float root = Mathf.Sqrt(discriminant);
            float enter = Mathf.Max(-b - root, 0f);
            float exit = Mathf.Min(-b + root, maxDistance);
            return Mathf.Max(exit - enter, 0f);
        }

        /// <summary>
        /// The share of the scene colour that survives fog at fog distance z, as the game's
        /// post-processing fog pass computes it (Unity Post-processing Stack v1).
        /// </summary>
        internal static float Transmittance(FogKind kind, float density, float start, float end, float z)
        {
            float t;
            switch (kind)
            {
                case FogKind.Linear:
                    t = end - start > 0f ? (end - z) / (end - start) : 1f;
                    break;
                case FogKind.Exponential:
                    t = Mathf.Pow(2f, -density * z);
                    break;
                default:
                    float f = density * z;
                    t = Mathf.Pow(2f, -f * f);
                    break;
            }
            return Mathf.Clamp01(t);
        }

        /// <summary>
        /// The share of the game's fog to keep on a pixel whose view ray spends
        /// <paramref name="clearFraction"/> of its length inside a bubble. The pixel is then
        /// drawn as lerp(unfogged, fogged, result), which leaves the game's own fog colour,
        /// sun glow included, untouched and only shortens the distance it builds up over.
        /// </summary>
        internal static float FogKept(FogKind kind, float density, float start, float end, float fogDistance, float clearFraction)
        {
            float fullFog = 1f - Transmittance(kind, density, start, end, fogDistance);
            if (fullFog <= 1e-4f)
            {
                return 1f;
            }

            float remaining = fogDistance * (1f - Mathf.Clamp01(clearFraction));
            float reducedFog = 1f - Transmittance(kind, density, start, end, remaining);
            return Mathf.Clamp01(reducedFog / fullFog);
        }
    }
}
