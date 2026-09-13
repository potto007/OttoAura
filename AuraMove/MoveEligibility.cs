using UnityEngine;

namespace OttoAura.AuraMove;

// MoveEligibility answers whether a placed piece may be moved by AuraMove and, if not, why.
// The rules are a direct port of OttoRedecorate.Redecorate.CanMove, minus the hammer/Feaster
// tool checks, which AuraMove replaces with its own Guild Move piece.
// Checks run in order; the first match wins.
internal enum MoveDenial
{
    None, NoPlayer, NotPlacedByPlayer, DeniedPrefab, BuildingPiece, Warded, Vehicle,
    Plant, Ward, ArmedTrap, ShieldGenerator, Stake, PrivateContainer, ContainerInUse, Supporting,
}

internal static class MoveEligibility
{
    internal static bool CanMove(Piece? piece, bool flashWard = false) =>
        Evaluate(piece, flashWard) == MoveDenial.None;

    internal static MoveDenial Evaluate(Piece? piece, bool flashWard = false)
    {
        // 1. Player must be alive. Build mode is no longer a bar: AuraMove is a hammer piece now,
        //    so every eligibility check runs with the hammer out.
        Player? localPlayer = Player.m_localPlayer;
        if (localPlayer == null || localPlayer.IsDead())
        {
            return MoveDenial.NoPlayer;
        }

        // 2. The piece must exist in the world and have been placed by a player.
        if (piece == null || piece.m_nview == null || !piece.m_nview.IsValid() || !piece.IsPlacedByPlayer())
        {
            return MoveDenial.NotPlacedByPlayer;
        }

        string prefabName = Utils.GetPrefabName(piece.gameObject.name);

        // 3. Explicitly allowed prefabs skip every later restriction.
        if (IsListed(OttoAuraPlugin.AuraMoveAllowedPrefabs.Value, prefabName))
        {
            return MoveDenial.None;
        }

        // 4. Explicitly denied prefabs are refused outright.
        if (IsListed(OttoAuraPlugin.AuraMoveDeniedPrefabs.Value, prefabName))
        {
            return MoveDenial.DeniedPrefab;
        }

        // 5. Building pieces, the walls, floors and beams filed under the workbench (2) and
        //    stonecutter (3) build tabs, stay put. Crafting stations (1) and furniture (4) pass.
        Piece.PieceCategory category = piece.m_category;
        if (category == Piece.PieceCategory.BuildingWorkbench || category == Piece.PieceCategory.BuildingStonecutter)
        {
            return MoveDenial.BuildingPiece;
        }

        // 6. Respect ward access. Pass flashWard through so the caller can make the ward ring on a
        //    rejected key press.
        if (!PrivateArea.CheckAccess(piece.transform.position, 0f, flashWard, false))
        {
            return MoveDenial.Warded;
        }

        // 7. Vehicles move under their own power; the Guild will not carry them.
        if (piece.GetComponent<Vagon>() || piece.GetComponent<Ship>())
        {
            return MoveDenial.Vehicle;
        }

        // 8. Ceiling-mounted pieces (hanging braziers, etc.) have no support concerns.
        if (piece.m_inCeilingOnly)
        {
            return MoveDenial.None;
        }

        // 9. Plants are rooted.
        if (piece.GetComponent<Plant>())
        {
            return MoveDenial.Plant;
        }

        // 10. Unowned or locally owned beds can be moved.
        if (piece.TryGetComponent<Bed>(out var bed) && (bed.GetOwner() == 0L || bed.GetOwner() == localPlayer.GetPlayerID()))
        {
            return MoveDenial.None;
        }

        // 11. A live ward is protecting the ground beneath it; moving it would drop coverage.
        if (piece.TryGetComponent<PrivateArea>(out var area) && area.IsEnabled())
        {
            return MoveDenial.Ward;
        }

        // 12. Armed traps are hazardous to relocate.
        if (piece.TryGetComponent<Trap>(out var trap) && trap.IsArmed())
        {
            return MoveDenial.ArmedTrap;
        }

        // 13. An active shield generator cannot be interrupted.
        if (piece.TryGetComponent<ShieldGenerator>(out var shield) && shield.m_radius > 0f)
        {
            return MoveDenial.ShieldGenerator;
        }

        // 14. Stakes and similar AoE weapons with attack settings are refused. Fireplaces share the
        //     Aoe component but are benign, so they are excluded from this check.
        Aoe aoe = piece.GetComponentInChildren<Aoe>();
        if (aoe && aoe.m_useAttackSettings && !piece.GetComponent<Fireplace>())
        {
            return MoveDenial.Stake;
        }

        // 15. Containers require access, and an open or in-use chest cannot be safely relocated.
        if (piece.TryGetComponent<Container>(out var container))
        {
            if (!container.CheckAccess(localPlayer.GetPlayerID()))
            {
                return MoveDenial.PrivateContainer;
            }

            if (container.IsInUse() || (container.m_open != null && container.m_open.activeSelf))
            {
                return MoveDenial.ContainerInUse;
            }
        }

        // 16. When Support Is Immovable is on, pieces that carry load stay where they are, unless
        //     they are furniture or crafting stations. A workbench or forge may report m_supports,
        //     but nobody builds a roof on one, and the owner wants every crafting table movable.
        if (OttoAuraPlugin.AuraMoveSupportImmovable.Value == OttoAuraPlugin.Toggle.On
            && piece.m_category != Piece.PieceCategory.Furniture
            && piece.m_category != Piece.PieceCategory.Crafting
            && !piece.GetComponent<CraftingStation>()
            && !piece.GetComponent<StationExtension>()
            && piece.TryGetComponent<WearNTear>(out var wnt)
            && wnt.m_supports)
        {
            return MoveDenial.Supporting;
        }

        return MoveDenial.None;
    }

    internal static string DenialMessage(MoveDenial denial) => denial switch
    {
        MoveDenial.None             => "",
        MoveDenial.NoPlayer         => "",
        MoveDenial.NotPlacedByPlayer => "The Guild only moves things a player put there.",
        MoveDenial.DeniedPrefab     => "The Guild does not move that.",
        MoveDenial.BuildingPiece    => "The Guild will not pull a building piece out of a building.",
        MoveDenial.Warded           => "Another jarl's ward holds this in place.",
        MoveDenial.Vehicle          => "The Guild does not move vehicles.",
        MoveDenial.Plant            => "Let the plant grow where it stands.",
        MoveDenial.Ward             => "The Guild will not move a ward.",
        MoveDenial.ArmedTrap        => "Disarm the trap before the Guild will touch it.",
        MoveDenial.ShieldGenerator  => "The shield generator cannot be moved while active.",
        MoveDenial.Stake            => "The Guild will not move an armed stake.",
        MoveDenial.PrivateContainer => "That chest belongs to someone else.",
        MoveDenial.ContainerInUse   => "Someone has it open.",
        MoveDenial.Supporting       => "It is holding up the building.",
        _                           => "",
    };

    // Parse the csv config on every call. The mod has a config file watcher that calls Config.Reload
    // at runtime, so a stale cache would silently serve the old list. A simple split is cheap enough
    // for a per-frame hover check.
    internal static bool IsListed(string csv, string prefabName)
    {
        if (string.IsNullOrEmpty(csv))
        {
            return false;
        }

        foreach (string entry in csv.Split(','))
        {
            if (entry.Trim() == prefabName)
            {
                return true;
            }
        }

        return false;
    }
}
