using System;
using System.Reflection;
using HarmonyLib;

namespace OttoAura;

// OttoPay is a hard dependency, but its API is still found at runtime so the build does not
// need OttoPay.dll. If the API is missing, AuraPay reads as off and nothing can be paid.
internal static class OttoPayBridge
{
    private static bool _bound;
    private static MethodInfo? _isAuraPayEnabled;
    private static MethodInfo? _tryWithdraw;
    private static MethodInfo? _registerAuraService;
    private static MethodInfo? _unregisterAuraService;
    private static MethodInfo? _tryDeposit;
    private static MethodInfo? _registerDepositHandler;
    private static MethodInfo? _unregisterDepositHandler;

    internal static bool IsAuraPayEnabled()
    {
        Bind();
        return _isAuraPayEnabled != null && (bool)_isAuraPayEnabled.Invoke(null, null);
    }

    // Takes the whole amount or nothing, the same as OttoPay does.
    internal static bool TryWithdraw(int amount)
    {
        Bind();
        return _tryWithdraw != null && (bool)_tryWithdraw.Invoke(null, new object[] { amount });
    }

    // True once OttoPay's deposit API and deposit handler hook are both there. They arrived in
    // OttoPay 1.6.0; without them AuraTrade stays dormant and nothing else changes.
    internal static bool CanDeposit
    {
        get
        {
            Bind();
            return _tryDeposit != null && _registerDepositHandler != null;
        }
    }

    // Offers OttoPay's balance icon an item it does not take itself. OttoPay shows its deposit
    // arrow while canDeposit answers true for the dragged item, titles the arrow's tooltip with
    // name over describe's text, and calls deposit when the item is dropped there. deposit owns
    // the whole sale, removal and credit both, and answers true only when both happened. Every
    // callback receives the drag's inventory, item and amount. No-op without OttoPay 1.6.0.
    internal static void RegisterDepositHandler(
        string name,
        Func<Inventory, ItemDrop.ItemData, int, bool> canDeposit,
        Func<Inventory, ItemDrop.ItemData, int, bool> deposit,
        Func<Inventory, ItemDrop.ItemData, int, string> describe)
    {
        Bind();
        if (!CanDeposit)
        {
            return;
        }
        _registerDepositHandler!.Invoke(null, new object[] { name, canDeposit, deposit, describe });
    }

    internal static void UnregisterDepositHandler(string name)
    {
        Bind();
        _unregisterDepositHandler?.Invoke(null, new object[] { name });
    }

    // Credits the whole amount or nothing: false for a non-member, a negative amount, or a balance
    // that would overflow. Zero is a successful no-op.
    internal static bool TryDeposit(int amount)
    {
        Bind();
        return _tryDeposit != null && (bool)_tryDeposit.Invoke(null, new object[] { amount });
    }

    // Registers a named service with the OttoPay 1.5.0 tooltip API. The describe delegate is
    // called lazily by OttoPay's UI and may return an empty string to hide the entry.
    // No-op when the API is absent; the AuraPay panel simply won't list OttoAura's services.
    internal static void RegisterAuraService(string name, Func<string> describe)
    {
        Bind();
        _registerAuraService?.Invoke(null, new object[] { name, describe });
    }

    internal static void UnregisterAuraService(string name)
    {
        Bind();
        _unregisterAuraService?.Invoke(null, new object[] { name });
    }

    private static void Bind()
    {
        if (_bound)
        {
            return;
        }

        _bound = true;
        Type? api = AccessTools.TypeByName("OttoPay.OttoPayApi");
        _isAuraPayEnabled = api == null ? null : AccessTools.Method(api, "IsAuraPayEnabled");
        _tryWithdraw = api == null ? null : AccessTools.Method(api, "TryWithdraw");
        if (_isAuraPayEnabled == null || _tryWithdraw == null)
        {
            _isAuraPayEnabled = null;
            _tryWithdraw = null;
            OttoAuraPlugin.OttoAuraLogger.LogWarning("OttoPay's AuraPay API was not found. Paid aura repairs and AuraBoost are off.");
        }

        // Tooltip registration API is optional: added in OttoPay 1.5.0. Missing it never disables
        // AuraPay or withdrawals; the AuraPay tooltip will just not list OttoAura's services.
        _registerAuraService = api == null ? null : AccessTools.Method(api, "RegisterAuraService");
        _unregisterAuraService = api == null ? null : AccessTools.Method(api, "UnregisterAuraService");
        if (_registerAuraService == null || _unregisterAuraService == null)
        {
            _registerAuraService = null;
            _unregisterAuraService = null;
            OttoAuraPlugin.OttoAuraLogger.LogWarning("OttoPay's RegisterAuraService API was not found. The AuraPay tooltip will not list OttoAura's services.");
        }

        // The deposit API is optional too: added in OttoPay 1.6.0 for AuraTrade. Missing any part of
        // it only keeps AuraTrade dormant.
        _tryDeposit = api == null ? null : AccessTools.Method(api, "TryDeposit", new[] { typeof(int) });
        _registerDepositHandler = api == null ? null : AccessTools.Method(api, "RegisterDepositHandler");
        _unregisterDepositHandler = api == null ? null : AccessTools.Method(api, "UnregisterDepositHandler");
        if (_tryDeposit == null || _registerDepositHandler == null || _unregisterDepositHandler == null)
        {
            _tryDeposit = null;
            _registerDepositHandler = null;
            _unregisterDepositHandler = null;
            OttoAuraPlugin.OttoAuraLogger.LogWarning("OttoPay's deposit API was not found. AuraTrade stays off until OttoPay 1.6.0 is installed.");
        }
    }
}
