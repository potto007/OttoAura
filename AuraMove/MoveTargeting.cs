using BepInEx.Configuration;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace OttoAura.AuraMove;

// The keyboard shortcut and the crosshair prompt. Aiming, rotation and hover detection all belong
// to the vanilla hammer now, so this is only the shortcut that opens the service and the line of
// text that tells the player what a click will do.
internal static class MoveTargeting
{
    // KeyLabel cache: avoid rebuilding every frame. The binding is the whole cache key, so a
    // rebind through the config file shows up on the next frame.
    private static string _cachedKeyLabel = "";
    private static KeyboardShortcut _cachedShortcut;

    // True when AuraMove must ignore the shortcut: a menu, the console, the chat box, the build
    // menu or the large map has the keyboard. Build mode is not blocked - that is where AuraMove
    // now lives.
    internal static bool InputBlocked
    {
        get
        {
            Player? player = Player.m_localPlayer;
            if (player == null || player.IsDead() || player.IsTeleporting())
            {
                return true;
            }

            if (Menu.IsVisible() || InventoryGui.IsVisible() || TextInput.IsVisible() || Console.IsVisible())
            {
                return true;
            }

            if (Chat.instance != null && Chat.instance.HasFocus())
            {
                return true;
            }

            if (Hud.IsPieceSelectionVisible())
            {
                return true;
            }

            if (Minimap.instance != null && Minimap.instance.m_mode == Minimap.MapMode.Large)
            {
                return true;
            }

            return false;
        }
    }

    // True on the frame the AuraMove shortcut fires and AuraMove is ready to act. Also read by the
    // Minimap.SetMapMode prefix, because vanilla's "Map" button fires on Key.M with no altKey
    // guard and would open the large map on the same frame.
    internal static bool ShortcutDown =>
        AuraMoveController.IsAvailable
        && !InputBlocked
        && OttoAuraPlugin.AuraMoveKey.Value.IsDown();

    // Human-readable label for the binding, for the crosshair prompt and the AuraPay tooltip.
    internal static string KeyLabel
    {
        get
        {
            KeyboardShortcut shortcut = OttoAuraPlugin.AuraMoveKey.Value;
            if (_cachedShortcut.Equals(shortcut) && _cachedKeyLabel.Length > 0)
            {
                return _cachedKeyLabel;
            }

            _cachedShortcut = shortcut;
            _cachedKeyLabel = shortcut.ToString();
            return _cachedKeyLabel;
        }
    }
}

// Say what a click will do while the hammer's Guild Move piece is selected.
[HarmonyPatch(typeof(Hud), nameof(Hud.UpdateCrosshair))]
static class Hud_UpdateCrosshair_Patch
{
    static void Postfix(Hud __instance)
    {
        Player? player = Player.m_localPlayer;
        if (player == null || !AuraMoveController.IsAvailable || !GuildMove.IsSelectedBy(player))
        {
            return;
        }

        TextMeshProUGUI? label = __instance.m_hoverName;
        if (label == null)
        {
            return;
        }

        string offer;
        if (MoveCarry.IsCarrying)
        {
            offer = $"Left click to set the {MoveCarry.CarriedName} down, right click and the Guild lets go";
        }
        else
        {
            Piece? piece = player.GetHoveringPiece();
            if (piece == null || !MoveEligibility.CanMove(piece))
            {
                return;
            }

            int cost = OttoAuraPlugin.AuraMoveCoins.Value;
            offer = cost > 0
                ? $"Left click and the Guild will move this for {cost} coins"
                : "Left click and the Guild will move this";
        }

        string line = $"<color=#8FD7FF>{offer}</color>";

        // UpdateCrosshair rebuilds the label every frame, so this appends once per frame. The
        // EndsWith guard keeps a frame that skipped the rebuild from stacking the prompt up.
        string existing = label.text;
        if (existing.EndsWith(line, System.StringComparison.Ordinal))
        {
            return;
        }

        label.text = existing.Length > 0 ? $"{existing}\n{line}" : line;
    }
}
