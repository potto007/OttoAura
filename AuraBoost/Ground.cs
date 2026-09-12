using UnityEngine;

namespace OttoAura.AuraBoost;

internal enum Footing
{
    Wild,
    Trail,
    Road,
}

// Reads what the local player is standing on from the ground contact Character already tracks,
// so nothing has to follow footsteps.
internal static class Ground
{
    // Terrain paint keeps one channel per tool: red for hoe dirt, green for cultivation, blue for
    // paving. Deep North deep snow is painted into all three at once, so a channel only counts
    // when it is clearly ahead of the other two.
    private const float ChannelLead = 0.35f;

    private static int _sampledFrame = -1;
    private static Footing _sampled;

    // The stamina hook and the status icon both ask every frame, so the answer is kept per frame.
    internal static Footing Under(Player player)
    {
        if (_sampledFrame != Time.frameCount)
        {
            _sampledFrame = Time.frameCount;
            _sampled = Sample(player);
        }

        return _sampled;
    }

    private static Footing Sample(Player player)
    {
        Collider? contact = player.m_lastGroundCollider;
        if (contact == null || player.InWater() || player.InLiquid())
        {
            return Footing.Wild;
        }

        Heightmap? terrain = contact.GetComponent<Heightmap>();
        if (terrain != null)
        {
            return FromPaint(terrain.GetPaintMask(player.m_lastGroundPoint));
        }

        WearNTear? piece = contact.GetComponentInParent<WearNTear>();
        return piece != null ? FromPiece(piece.m_materialType) : Footing.Wild;
    }

    // Only things a player built count. Boulders, roots and ice floes are not roads.
    private static Footing FromPiece(WearNTear.MaterialType material) => material switch
    {
        WearNTear.MaterialType.Stone or WearNTear.MaterialType.Marble or WearNTear.MaterialType.Ashstone or WearNTear.MaterialType.Ancient => Footing.Road,
        WearNTear.MaterialType.Wood or WearNTear.MaterialType.HardWood or WearNTear.MaterialType.Timberwood or WearNTear.MaterialType.Iron => Footing.Trail,
        _ => Footing.Wild,
    };

    private static Footing FromPaint(Color paint)
    {
        if (paint.b - Mathf.Max(paint.r, paint.g) >= ChannelLead)
        {
            return Footing.Road;
        }

        return paint.r - Mathf.Max(paint.g, paint.b) >= ChannelLead ? Footing.Trail : Footing.Wild;
    }
}
