using BepInEx.Configuration;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace OttoAura.AuraMove;

// Hover raycast, input detection, rotation accumulation, and the crosshair prompt for AuraMove.
// Owns no state machine - just what the player is pointing at and what keys they are pressing.
internal static class MoveTargeting
{
    private static Piece? _hovered;

    // Gamepad repeat-input state for rotation.
    private static float _rotScrollAccum;
    private static float _rotGamepadHeld;       // seconds the button has been held
    private static int _rotGamepadLastDir;       // +1 or -1, 0 when no button held
    private static float _rotGamepadNextRepeat;

    // Single-warning guard: emit the "unknown ZInput button" warning at most once per session.
    private static bool _zinputWarned;

    // KeyLabel cache: avoid rebuilding every frame. Every input the label is built from is part
    // of the cache key, so a rebind through the config file shows up on the next frame.
    private static string _cachedKeyLabel = "";
    private static string _cachedModifier = "";
    private static string _cachedButton = "";
    private static KeyboardShortcut _cachedShortcut;
    private static bool _cachedGamepadActive;

    private const float GamepadRepeatDelay = 0.25f;
    private const float GamepadRepeatInterval = 0.17f;

    // ---- Public API -------------------------------------------------------

    internal static Piece? Hovered => _hovered;

    // True on the frame the AuraMove keyboard shortcut fires and AuraMove is ready to act.
    // Used by the Minimap.SetMapMode prefix to suppress the Large map when Alt+M grabs a piece:
    // vanilla's "Map" button fires on Key.M with no altKey guard, so Minimap.Update would open
    // the large map on the same frame that AuraMove receives its grab key.
    internal static bool KeyboardShortcutDown =>
        AuraMoveController.IsAvailable
        && !InputBlocked
        && OttoAuraPlugin.AuraMoveKey.Value.IsDown();

    // True when AuraMove must ignore all input: menus open, build mode, dead/teleporting, etc.
    internal static bool InputBlocked
    {
        get
        {
            Player? player = Player.m_localPlayer;
            if (player == null || player.IsDead() || player.IsTeleporting() || player.InPlaceMode())
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

    // Human-readable label for the active binding, rebuilt only when the config or gamepad state changes.
    internal static string KeyLabel
    {
        get
        {
            bool gpActive = ZInput.IsGamepadActive();
            string mod = OttoAuraPlugin.AuraMoveGamepadModifier.Value;
            string btn = OttoAuraPlugin.AuraMoveGamepadButton.Value;
            KeyboardShortcut shortcut = OttoAuraPlugin.AuraMoveKey.Value;

            if (_cachedGamepadActive == gpActive && _cachedModifier == mod && _cachedButton == btn
                && _cachedShortcut.Equals(shortcut) && _cachedKeyLabel.Length > 0)
            {
                return _cachedKeyLabel;
            }

            _cachedGamepadActive = gpActive;
            _cachedModifier = mod;
            _cachedButton = btn;
            _cachedShortcut = shortcut;
            _cachedKeyLabel = BuildKeyLabel(gpActive, mod, btn, shortcut);
            return _cachedKeyLabel;
        }
    }

    // Cast from the camera into the world and store the closest Piece in _hovered.
    internal static void UpdateHover()
    {
        Player? player = Player.m_localPlayer;
        if (player == null)
        {
            ClearHover();
            return;
        }

        if (GameCamera.instance == null)
        {
            ClearHover();
            return;
        }

        if (!Physics.Raycast(GameCamera.instance.transform.position, GameCamera.instance.transform.forward,
                out RaycastHit hit, 50f, player.m_removeRayMask))
        {
            ClearHover();
            return;
        }

        if (Vector3.Distance(player.m_eye.position, hit.point) >= player.m_maxPlaceDistance)
        {
            ClearHover();
            return;
        }

        _hovered = hit.collider.GetComponentInParent<Piece>();
    }

    internal static void ClearHover()
    {
        _hovered = null;
    }

    // True on the frame the player presses the grab/confirm key. False when input is blocked.
    internal static bool ActivatePressed()
    {
        if (InputBlocked)
        {
            return false;
        }

        // Keyboard path.
        KeyboardShortcut ksc = OttoAuraPlugin.AuraMoveKey.Value;
        if (ksc.IsDown())
        {
            return true;
        }

        // Gamepad path - only when a gamepad is active.
        if (!ZInput.IsGamepadActive())
        {
            return false;
        }

        string modifier = OttoAuraPlugin.AuraMoveGamepadModifier.Value;
        string button = OttoAuraPlugin.AuraMoveGamepadButton.Value;

        if (string.IsNullOrWhiteSpace(button))
        {
            return false;
        }

        WarnOnceIfUnknownButton(button);

        bool modifierHeld = true;
        if (!string.IsNullOrWhiteSpace(modifier))
        {
            WarnOnceIfUnknownButton(modifier);
            modifierHeld = ZInput.GetButton(modifier);
        }

        if (!modifierHeld || !ZInput.GetButtonDown(button))
        {
            return false;
        }

        // Vanilla does not gate its own gamepad actions on the alt-key layer: the default
        // JoyButtonY also opens the inventory (InventoryGui.Update). Spend the press the way
        // vanilla spends its own so the grab does not come with a full-screen inventory.
        if (ZInput.instance != null)
        {
            ZInput.ResetButtonStatus(button);
        }

        return true;
    }

    // A misspelled button name in the config silently never fires, so say so once per session.
    private static void WarnOnceIfUnknownButton(string name)
    {
        if (_zinputWarned || ZInput.instance == null || ZInput.instance.m_buttons.ContainsKey(name))
        {
            return;
        }

        OttoAuraPlugin.OttoAuraLogger.LogWarning($"AuraMove: '{name}' is not a ZInput button, so AuraMove can never fire on a gamepad.");
        _zinputWarned = true;
    }

    // Cancel works with a lighter guard than InputBlocked: any live, non-dead local player.
    internal static bool CancelPressed()
    {
        Player? player = Player.m_localPlayer;
        if (player == null || player.IsDead())
        {
            return false;
        }

        if (ZInput.GetButtonDown("Escape"))
        {
            return true;
        }

        if (ZInput.IsGamepadActive() && ZInput.GetButtonDown("JoyButtonB"))
        {
            return true;
        }

        return false;
    }

    // Returns the signed number of Player.m_placeRotationDegrees steps requested this frame and
    // resets the accumulator. Mirrors the vanilla rotation input in Player.UpdatePlacement.
    internal static int ConsumeRotationSteps()
    {
        if (InputBlocked)
        {
            _rotScrollAccum = 0f;
            _rotGamepadHeld = 0f;
            _rotGamepadLastDir = 0;
            return 0;
        }

        int steps = 0;

        // Mouse wheel: accumulate until the threshold is crossed.
        // A zero threshold would spin the accumulator loops forever, so never trust it blindly.
        Player? player = Player.m_localPlayer;
        float threshold = player != null && player.m_scrollAmountThreshold > 0f ? player.m_scrollAmountThreshold : 0.1f;

        _rotScrollAccum += ZInput.GetMouseScrollWheel();
        while (_rotScrollAccum >= threshold)
        {
            steps++;
            _rotScrollAccum -= threshold;
        }
        while (_rotScrollAccum <= -threshold)
        {
            steps--;
            _rotScrollAccum += threshold;
        }

        // Gamepad rotate buttons: JoyRotate is +1, JoyRotateRight is -1 (vanilla convention).
        // On the classic gamepad layout JoyAltKeys and JoyRotate are both the left trigger, so
        // holding the move modifier would spin the ghost a step on every press. Treat the
        // modifier as the alt layer vanilla means it to be and read no rotation while it is held.
        string rotationModifier = OttoAuraPlugin.AuraMoveGamepadModifier.Value;
        bool altLayerHeld = !string.IsNullOrWhiteSpace(rotationModifier) && ZInput.GetButton(rotationModifier);

        int dir = 0;
        if (ZInput.IsGamepadActive() && !altLayerHeld)
        {
            if (ZInput.GetButton("JoyRotate"))
            {
                dir = 1;
            }
            else if (ZInput.GetButton("JoyRotateRight"))
            {
                dir = -1;
            }
        }

        if (dir != 0)
        {
            if (dir != _rotGamepadLastDir)
            {
                // First press this direction: emit one step immediately, start hold timer.
                steps += dir;
                _rotGamepadHeld = 0f;
                _rotGamepadNextRepeat = GamepadRepeatDelay;
                _rotGamepadLastDir = dir;
            }
            else
            {
                _rotGamepadHeld += Time.deltaTime;
                if (_rotGamepadHeld >= _rotGamepadNextRepeat)
                {
                    steps += dir;
                    _rotGamepadNextRepeat = _rotGamepadHeld + GamepadRepeatInterval;
                }
            }
        }
        else
        {
            _rotGamepadLastDir = 0;
            _rotGamepadHeld = 0f;
        }

        return steps;
    }

    // ---- Private helpers --------------------------------------------------

    private static string BuildKeyLabel(bool gamepadActive, string modifier, string button, KeyboardShortcut shortcut)
    {
        if (gamepadActive)
        {
            string modPart = string.IsNullOrWhiteSpace(modifier) ? "" : StripJoyPrefix(modifier);
            string btnPart = StripJoyPrefix(button);
            return string.IsNullOrEmpty(modPart) ? btnPart : $"{modPart} + {btnPart}";
        }

        // Keyboard: BepInEx renders the main key first, then the modifiers.
        return shortcut.ToString();
    }

    // Strip a leading "Joy" prefix so "JoyAltKeys" -> "AltKeys" and "JoyButtonY" -> "ButtonY".
    private static string StripJoyPrefix(string name)
    {
        return name.StartsWith("Joy", System.StringComparison.Ordinal) ? name.Substring(3) : name;
    }
}

// Append a Guild move prompt to the crosshair hover text whenever the hovered piece is movable.
[HarmonyPatch(typeof(Hud), nameof(Hud.UpdateCrosshair))]
static class Hud_UpdateCrosshair_Patch
{
    static void Postfix(Hud __instance)
    {
        if (!AuraMoveController.IsAvailable || AuraMoveController.IsMoving)
        {
            return;
        }

        Piece? piece = MoveTargeting.Hovered;
        if (piece == null || !MoveEligibility.CanMove(piece))
        {
            return;
        }

        TextMeshProUGUI? label = __instance.m_hoverName;
        if (label == null)
        {
            return;
        }

        int cost = OttoAuraPlugin.AuraMoveCoins.Value;
        string offer = cost > 0
            ? $"The Guild will move this for {cost} coins [{MoveTargeting.KeyLabel}]"
            : $"The Guild will move this [{MoveTargeting.KeyLabel}]";
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
