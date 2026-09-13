using HarmonyLib;
using UnityEngine;

namespace OttoAura.AuraMove;

// AuraMoveController: the state machine behind the hammer's Merchant Guild tab. It keeps the
// pseudo-piece's availability in step with the config and with AuraPay, answers the left click
// that takes hold of an object or sets it down, charges the fee and sends the relocation RPC.
//
// IsAvailable is re-read every frame because the server can switch the setting and the player can
// switch AuraPay at any moment - the same pattern as AuraBoostEffect.IsActive.
internal static class AuraMoveController
{
    private static bool _lastAvailable;

    // The server can switch AuraMove off, and the player can switch AuraPay off, at any moment.
    internal static bool IsAvailable =>
        OttoAuraPlugin.AuraMoveEnabled.Value == OttoAuraPlugin.Toggle.On && OttoPayBridge.IsAuraPayEnabled();

    // Called from Plugin.Start. No world, ZNet or ZNetScene exists yet; only clear local state.
    internal static void Init()
    {
        MoveCarry.Drop(rebuildGhost: false);
        _lastAvailable = false;
    }

    internal static void Tick()
    {
        Player? player = Player.m_localPlayer;
        if (player == null)
        {
            MoveCarry.Drop(rebuildGhost: false);
            _lastAvailable = false;
            return;
        }

        bool available = IsAvailable;

        // The carry goes first: dropping it before the piece list is rebuilt keeps vanilla from
        // building one more ghost of an object the Guild has already let go of.
        if (MoveCarry.IsCarrying && !CarryStillValid(player, available))
        {
            // Any interrupt ends the move silently. Nothing was charged.
            MoveCarry.Drop(rebuildGhost: true);
        }

        if (available != _lastAvailable)
        {
            _lastAvailable = available;
            // Rebuild the hammer's piece list so the Merchant Guild tab appears or disappears the
            // moment the setting or AuraPay changes, rather than at the next equipment change.
            if (player.InPlaceMode())
            {
                player.UpdateAvailablePiecesList();
            }
        }

        GuildMove.RefreshDescription();

        if (player.InPlaceMode() && player.m_buildPieces == GuildMove.HammerTable)
        {
            GuildMove.EnsureTabs(player.m_buildPieces);
        }

        HandleShortcut(player, available);
    }

    internal static void Shutdown()
    {
        MoveCarry.Drop(rebuildGhost: false);
        MoveEffects.StopAll();
        GuildMove.Shutdown();
        _lastAvailable = false;
    }

    // Left click with Guild Move selected. Returns true to hand the click back to vanilla, which
    // is only ever done so vanilla can report why a destination was refused.
    internal static bool HandlePlaceClick(Player player)
    {
        if (!IsAvailable)
        {
            MoveCarry.Drop(rebuildGhost: true);
            return false;
        }

        if (!MoveCarry.IsCarrying)
        {
            TryGrab(player);
            return false;
        }

        // Vanilla decides first: rotation, snapping, wards, biome and clipping all come from
        // UpdatePlacementGhost, and the distance rule is applied on top of it.
        player.UpdatePlacementGhost(flashGuardStone: true);

        if (MoveCarry.TooFar)
        {
            player.Message(MessageHud.MessageType.TopLeft, "The Guild will not carry it that far.");
            return false;
        }

        if (player.m_placementStatus != Player.PlacementStatus.Valid)
        {
            // Vanilla's own message names the actual reason.
            return true;
        }

        ChargeAndCommit(player);
        return false;
    }

    // Cancel on the player's own right click.
    internal static void CancelByPlayer(Player player)
    {
        player.Message(MessageHud.MessageType.TopLeft, "The Merchant Guild lets go.");
        MoveCarry.Drop(rebuildGhost: true);
    }

    private static void TryGrab(Player player)
    {
        Piece? hovered = player.GetHoveringPiece();
        if (hovered == null)
        {
            // Clicking at open air is not a request for anything, so say nothing.
            return;
        }

        // flashWard: true so a warded refusal flashes the ward on a deliberate click.
        MoveDenial denial = MoveEligibility.Evaluate(hovered, flashWard: true);
        if (denial != MoveDenial.None)
        {
            string reason = MoveEligibility.DenialMessage(denial);
            if (reason.Length > 0)
            {
                player.Message(MessageHud.MessageType.TopLeft, reason);
            }
            return;
        }

        if (!MoveCarry.Grab(player, hovered))
        {
            return;
        }

        player.Message(MessageHud.MessageType.TopLeft, $"The Merchant Guild takes hold of the {MoveCarry.CarriedName}.");
    }

    private static void ChargeAndCommit(Player player)
    {
        Piece? source = MoveCarry.Source;
        GameObject? ghost = player.m_placementGhost;

        // Never take coins for a move that cannot be sent: check the piece and the network layer
        // first, because TryWithdraw has no refund.
        if (source == null || ghost == null || !MoveRelocation.CanRequest(source))
        {
            MoveCarry.Drop(rebuildGhost: true);
            return;
        }

        // Eligibility was judged when the player took hold, and aiming takes time: another player
        // can open the chest, a ward can be switched on, a trap can be armed. Judge it again on
        // the frame the coins would move.
        MoveDenial denial = MoveEligibility.Evaluate(source, flashWard: true);
        if (denial != MoveDenial.None)
        {
            string denialReason = MoveEligibility.DenialMessage(denial);
            if (denialReason.Length > 0)
            {
                player.Message(MessageHud.MessageType.TopLeft, denialReason);
            }
            MoveCarry.Drop(rebuildGhost: true);
            return;
        }

        int cost = Mathf.Max(0, OttoAuraPlugin.AuraMoveCoins.Value);
        if (cost > 0 && !OttoPayBridge.TryWithdraw(cost))
        {
            // TryWithdraw is whole-or-nothing; nothing was taken.
            // Answer this deliberate click every time - do not throttle.
            player.Message(MessageHud.MessageType.TopLeft,
                "Your Merchant Bank balance is empty. The Merchant Guild does not work for free.");
            return; // Stay in the move; the player can try again after topping up.
        }

        Vector3 position = ghost.transform.position;
        Quaternion rotation = ghost.transform.rotation;
        string name = MoveCarry.CarriedName;

        bool sent = MoveRelocation.Request(source, position, rotation);

        // End the move before showing any message.
        MoveCarry.Drop(rebuildGhost: true);

        if (!sent)
        {
            // The coins are already gone; we cannot refund. Log and stay quiet to the player.
            OttoAuraPlugin.OttoAuraLogger.LogWarning("AuraMove: the move RPC could not be sent after the fee was taken.");
            return;
        }

        string moved = cost > 0
            ? $"The Guild moves the {name} for {cost} coins."
            : $"The Guild moves the {name}.";
        player.Message(MessageHud.MessageType.TopLeft, moved);
    }

    // Everything that has to stay true for the whole of a carry. The explicit hooks in MoveCarry
    // catch the ordinary exits; this catches the rest, including the source object being
    // destroyed or unloaded under the player.
    private static bool CarryStillValid(Player player, bool available)
    {
        if (!available || player.IsDead() || player.IsTeleporting() || !player.InPlaceMode())
        {
            return false;
        }

        if (player.m_buildPieces != GuildMove.HammerTable || !GuildMove.IsSelectedBy(player))
        {
            return false;
        }

        Piece? source = MoveCarry.Source;
        if (source == null || source.m_nview == null || !source.m_nview.IsValid())
        {
            return false;
        }

        // Walking away from the object ends the move: the destination can never be further from
        // the object than Max Move Distance, so there is nothing left to aim at.
        return Vector3.Distance(player.transform.position, source.transform.position)
               <= OttoAuraPlugin.AuraMoveMaxDistance.Value + player.m_maxPlaceDistance;
    }

    // The shortcut equips the hammer and selects Guild Move, and puts the hammer away again when
    // Guild Move is already what is selected.
    private static void HandleShortcut(Player player, bool available)
    {
        if (!MoveTargeting.ShortcutDown || !available)
        {
            return;
        }

        if (GuildMove.PseudoPiece == null || GuildMove.HammerTable == null)
        {
            return;
        }

        if (GuildMove.IsSelectedBy(player))
        {
            // Unequip rather than HideHandItems: hiding only stows the hammer, and the next
            // interaction brings it straight back out. UnequipItem runs SetupEquipment, which
            // leaves place mode the same way pressing the hotbar slot again does.
            MoveCarry.Drop(rebuildGhost: false);
            player.UnequipItem(player.GetRightItem());
            return;
        }

        if (player.m_buildPieces != GuildMove.HammerTable)
        {
            ItemDrop.ItemData? hammer = FindHammer(player);
            if (hammer == null)
            {
                player.Message(MessageHud.MessageType.TopLeft, "The Merchant Guild works through a hammer, and you have none.");
                return;
            }

            if (!player.EquipItem(hammer))
            {
                return;
            }
        }

        player.SetSelectedPiece(GuildMove.PseudoPiece);
    }

    // Any hammer, vanilla or modded, as long as it drives the hammer's own piece table.
    private static ItemDrop.ItemData? FindHammer(Player player)
    {
        foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
        {
            if (item.m_shared.m_buildPieces == GuildMove.HammerTable)
            {
                return item;
            }
        }

        return null;
    }
}

// Alt+M opens AuraMove, but vanilla registers its "Map" button on Key.M with altKey:false
// (ZInput line ~2997), and Minimap.Update calls SetMapMode(Large) on GetButtonDown("Map")
// regardless of Alt. Script execution order between OttoAura.Update and Minimap.Update is
// undefined, so a ResetButtonStatus consume alone is not enough to prevent both from firing on
// the same frame. Skip the Large map transition for exactly the frame that the AuraMove shortcut
// is down. Small and None are never blocked, so the player can always close the map.
[HarmonyPatch(typeof(Minimap), nameof(Minimap.SetMapMode))]
static class Minimap_SetMapMode_Patch
{
    private static bool Prefix(Minimap.MapMode mode)
    {
        return !(mode == Minimap.MapMode.Large && MoveTargeting.ShortcutDown);
    }
}

// Leaving the world tears the local player down without touching the build menu, so the carry has
// to be cleared here or it would still be held when the next world loads.
[HarmonyPatch(typeof(Game), nameof(Game.Logout))]
static class Game_Logout_Patch
{
    static void Prefix()
    {
        MoveCarry.Drop(rebuildGhost: false);
        MoveEffects.StopAll();
    }
}
