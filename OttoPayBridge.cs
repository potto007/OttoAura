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
    }
}
