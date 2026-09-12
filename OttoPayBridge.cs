using System;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace OttoAura;

// OttoPay is optional, so its API is found at runtime instead of referenced at build time.
// Without OttoPay, AuraPay reads as off and nothing can be paid.
internal static class OttoPayBridge
{
    private const string OttoPayGuid = "potto007.OttoPay";

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
        if (!Chainloader.PluginInfos.ContainsKey(OttoPayGuid))
        {
            return;
        }

        Type? api = AccessTools.TypeByName("OttoPay.OttoPayApi");
        _isAuraPayEnabled = api == null ? null : AccessTools.Method(api, "IsAuraPayEnabled");
        _tryWithdraw = api == null ? null : AccessTools.Method(api, "TryWithdraw");
        if (_isAuraPayEnabled == null || _tryWithdraw == null)
        {
            _isAuraPayEnabled = null;
            _tryWithdraw = null;
            OttoAuraPlugin.OttoAuraLogger.LogWarning("OttoPay is installed, but its AuraPay API was not found. Paid aura repairs and AuraBoost are off.");
        }
    }
}
