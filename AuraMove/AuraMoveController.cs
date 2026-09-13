using HarmonyLib;
using UnityEngine;

namespace OttoAura.AuraMove;

// AuraMoveController: the state machine that reads input, drives the placement ghost, charges the
// fee and sends the relocation RPC. IsAvailable is re-read every frame because the server can
// switch the setting and the player can switch AuraPay at any moment - the same pattern as
// AuraBoostEffect.IsActive.
internal static class AuraMoveController
{
    private static Piece? _selected;
    private static bool _isMoving;

    // Frame on which an Escape press was spent cancelling a move. See Menu_Show_Patch.
    private static int _menuSuppressFrame = -1;

    // The server can switch AuraMove off, and the player can switch AuraPay off, at any moment.
    internal static bool IsAvailable =>
        OttoAuraPlugin.AuraMoveEnabled.Value == OttoAuraPlugin.Toggle.On && OttoPayBridge.IsAuraPayEnabled();

    internal static bool IsMoving => _isMoving;
    internal static Piece? Selected => _selected;

    // Called from Plugin.Start. No world, ZNet or ZNetScene exists yet; only clear local state.
    internal static void Init()
    {
        _selected = null;
        _isMoving = false;
    }

    internal static void Tick()
    {
        Player? player = Player.m_localPlayer;

        if (player == null || !IsAvailable)
        {
            if (_isMoving)
            {
                // Server or player switched off while a move was in flight; cancel silently.
                EndMove();
            }
            MoveTargeting.ClearHover();
            return;
        }

        if (_isMoving)
        {
            TickMoving(player);
        }
        else
        {
            TickIdle(player);
        }
    }

    internal static void Shutdown()
    {
        _isMoving = false;
        _selected = null;
        MovePlacement.Cancel();
        MoveEffects.StopAll();
    }

    private static void TickMoving(Player player)
    {
        // Abort silently on any interrupt condition.
        if (player.IsDead()
            || player.IsTeleporting()
            || player.InPlaceMode()
            || _selected == null
            || _selected.m_nview == null
            || !_selected.m_nview.IsValid()
            || Vector3.Distance(player.transform.position, _selected.transform.position)
               > OttoAuraPlugin.AuraMoveMaxDistance.Value + player.m_maxPlaceDistance)
        {
            EndMove();
            return;
        }

        // Explicit cancel by the player.
        if (MoveTargeting.CancelPressed())
        {
            CancelByPlayer(player);
            return;
        }

        // A menu, the console or the large map has the camera and the pointer; freeze the ghost
        // where it is instead of letting it chase a camera the player is not aiming with.
        if (MoveTargeting.InputBlocked)
        {
            return;
        }

        MovePlacement.UpdateGhost(_selected);

        if (!MoveTargeting.ActivatePressed())
        {
            return;
        }

        if (!MovePlacement.IsValid)
        {
            // Tell the player and stay in the move so they can aim again.
            player.Message(MessageHud.MessageType.TopLeft, "The Guild will not set it down there.");
            return;
        }

        ChargeAndCommit(player);
    }

    private static void TickIdle(Player player)
    {
        // Nothing to point at while a menu is up or a build tool is out, and the crosshair prompt
        // must not linger there either.
        if (MoveTargeting.InputBlocked)
        {
            MoveTargeting.ClearHover();
            return;
        }

        MoveTargeting.UpdateHover();

        if (!MoveTargeting.ActivatePressed())
        {
            return;
        }

        // Pressing the key at open air is not a request for anything, so say nothing.
        Piece? hovered = MoveTargeting.Hovered;
        if (hovered == null)
        {
            return;
        }

        // flashWard: true so a warded refusal flashes the ward on a deliberate key press.
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

        if (!MovePlacement.Begin(hovered))
        {
            return;
        }

        _selected = hovered;
        _isMoving = true;
        string name = Localization.instance.Localize(hovered.m_name);
        player.Message(MessageHud.MessageType.TopLeft, $"The Merchant Guild takes hold of the {name}.");

        // Return immediately so this same key press cannot also confirm placement in this frame.
    }

    private static void ChargeAndCommit(Player player)
    {
        // Never take coins for a move that cannot be sent: check the piece and the network layer
        // first, because TryWithdraw has no refund.
        Piece? selected = _selected;
        if (selected == null || !MoveRelocation.CanRequest(selected))
        {
            EndMove();
            return;
        }

        // Eligibility was judged when the player took hold, and aiming takes time: another player
        // can open the chest, a ward can be switched on, a trap can be armed. Judge it again on
        // the frame the coins would move.
        MoveDenial denial = MoveEligibility.Evaluate(selected, flashWard: true);
        if (denial != MoveDenial.None)
        {
            string denialReason = MoveEligibility.DenialMessage(denial);
            if (denialReason.Length > 0)
            {
                player.Message(MessageHud.MessageType.TopLeft, denialReason);
            }
            EndMove();
            return;
        }

        int cost = Mathf.Max(0, OttoAuraPlugin.AuraMoveCoins.Value);
        if (cost > 0 && !OttoPayBridge.TryWithdraw(cost))
        {
            // TryWithdraw is whole-or-nothing; nothing was taken.
            // Answer this deliberate key press every time - do not throttle.
            player.Message(MessageHud.MessageType.TopLeft,
                "Your Merchant Bank balance is empty. The Merchant Guild does not work for free.");
            return; // Stay in the move; the player can try again after topping up.
        }

        Vector3 position = MovePlacement.Position;
        Quaternion rotation = MovePlacement.Rotation;
        string name = Localization.instance.Localize(selected.m_name);

        bool sent = MoveRelocation.Request(selected, position, rotation);

        // End the move before showing any message.
        EndMove();

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

    // Cancel on the player's own key press, and remember the frame so the pause menu can be kept
    // out of it.
    private static void CancelByPlayer(Player player)
    {
        _menuSuppressFrame = Time.frameCount;
        player.Message(MessageHud.MessageType.TopLeft, "The Merchant Guild lets go.");
        EndMove();
    }

    // True once for the frame a cancel spent the Escape press. Menu_Show_Patch is the only caller.
    internal static bool ConsumeMenuSuppression()
    {
        if (_menuSuppressFrame != Time.frameCount)
        {
            return false;
        }

        _menuSuppressFrame = -1;
        return true;
    }

    // Cancel from inside Menu.Show, for the frame ordering where Menu.Update reads the Escape
    // press before AuraMoveController.Tick does. Returns true when it took the press.
    internal static bool CancelForMenuKey()
    {
        Player? player = Player.m_localPlayer;
        if (!_isMoving || player == null || !MoveTargeting.CancelPressed())
        {
            return false;
        }

        CancelByPlayer(player);
        return true;
    }

    private static void EndMove()
    {
        _isMoving = false;
        _selected = null;
        MovePlacement.Cancel();
    }
}

// Escape is the cancel key, but Menu.Update reads KeyCode.Escape straight off the device, so no
// ZInput reset can hide the press from it and every cancel would also open the pause menu. This
// prefix eats the menu for exactly that press, whichever Update read the key first, and never
// touches Escape when no move is in flight.
[HarmonyPatch(typeof(Menu), nameof(Menu.Show))]
static class Menu_Show_Patch
{
    private static bool Prefix()
    {
        if (AuraMoveController.ConsumeMenuSuppression())
        {
            return false;
        }

        return !AuraMoveController.CancelForMenuKey();
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
        return !(mode == Minimap.MapMode.Large && MoveTargeting.KeyboardShortcutDown);
    }
}
