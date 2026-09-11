using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using SkillManager;
using UnityEngine;

namespace OttoAura;

// The aura is local player state only: health, durability and the bank balance all belong to
// the player running this client, so each client ticks for itself and no RPC is needed.
internal static class WardAura
{
    private const string OttoPayGuid = "potto007.OttoPay";

    private static readonly List<ItemDrop.ItemData> WornItems = new();
    private static float _elapsed;
    private static bool _payBound;
    private static MethodInfo? _isAuraPayEnabled;
    private static MethodInfo? _tryWithdraw;
    private static float _lastEmptyPouchMessage = -1000f;

    internal static void Update(float deltaTime)
    {
        Player? player = Player.m_localPlayer;
        if (player == null || player.IsDead())
        {
            _elapsed = 0f;
            return;
        }

        _elapsed += deltaTime;
        if (_elapsed < OttoAuraPlugin.TickSeconds.Value)
        {
            return;
        }

        float elapsed = _elapsed;
        _elapsed = 0f;

        if (FindAuraWard(player) == null)
        {
            return;
        }

        Heal(player, elapsed);
        Repair(player);
    }

    // An aura needs a player ward that is switched on, contains the player, and lists the
    // player as its creator or as permitted. That is the same access a ward grants to build.
    internal static PrivateArea? FindAuraWard(Player player)
    {
        Vector3 position = player.transform.position;
        foreach (PrivateArea area in PrivateArea.m_allAreas)
        {
            if (area == null || area.m_ownerFaction != Character.Faction.Players)
            {
                continue;
            }

            if (area.IsEnabled() && area.IsInside(position, 0f) && area.HaveLocalAccess())
            {
                return area;
            }
        }

        return null;
    }

    internal static bool IsPaidRepair => OttoAuraPlugin.CoinsPerItemTick.Value > 0;

    private static void Heal(Player player, float elapsed)
    {
        float amount = OttoAuraPlugin.HealPerSecond.Value * elapsed;
        if (amount <= 0f || player.GetHealth() >= player.GetMaxHealth())
        {
            return;
        }

        player.Heal(amount, OttoAuraPlugin.ShowHealText.Value == OttoAuraPlugin.Toggle.On);
    }

    private static void Repair(Player player)
    {
        float percent = OttoAuraPlugin.RepairPercentPerTick.Value;
        if (percent <= 0f)
        {
            return;
        }

        WornItems.Clear();
        player.GetInventory().GetWornItems(WornItems);
        WornItems.RemoveAll(item => !item.m_shared.m_canBeReparied);
        if (WornItems.Count == 0)
        {
            return;
        }

        if (IsPaidRepair && !TryPay(player, OttoAuraPlugin.CoinsPerItemTick.Value * WornItems.Count))
        {
            return;
        }

        foreach (ItemDrop.ItemData item in WornItems)
        {
            float max = item.GetMaxDurability();
            item.m_durability = Mathf.Min(max, item.m_durability + max * percent / 100f);
            if (item.m_durability >= max)
            {
                OnFullyRepaired(player, item);
            }
        }
    }

    private static void OnFullyRepaired(Player player, ItemDrop.ItemData item)
    {
        if (OttoAuraPlugin.BlacksmithingInstalled)
        {
            int minutesToSet = 0;
            float skillFactor = player.GetSkillFactor(Skill.fromName("Blacksmithing"));
            if (skillFactor >= 0.5f)
            {
                minutesToSet = (int)(10 * skillFactor);
            }

            item.m_customData["RepairStation"] = DateTime.Now.AddMinutes(minutesToSet).ToString(CultureInfo.InvariantCulture);
            item.m_customData["RepairStationUseDurability"] = item.m_shared.m_useDurability.ToString();
        }

        player.Message(MessageHud.MessageType.TopLeft, Localization.instance.Localize("$msg_repaired", item.m_shared.m_name));
        if (OttoAuraPlugin.craftingStationClone != null)
        {
            OttoAuraPlugin.craftingStationClone.m_repairItemDoneEffects.Create(player.transform.position, Quaternion.identity);
        }
    }

    private static bool TryPay(Player player, int cost)
    {
        BindOttoPay();
        if (_isAuraPayEnabled == null || _tryWithdraw == null)
        {
            return false;
        }

        if (!(bool)_isAuraPayEnabled.Invoke(null, null))
        {
            return false;
        }

        if ((bool)_tryWithdraw.Invoke(null, new object[] { cost }))
        {
            return true;
        }

        if (Time.time - _lastEmptyPouchMessage > 30f)
        {
            player.Message(MessageHud.MessageType.TopLeft, "Your Merchant Bank balance is empty. The aura cannot repair your gear.");
            _lastEmptyPouchMessage = Time.time;
        }

        return false;
    }

    // OttoPay is optional, so it is found at runtime instead of referenced at build time.
    private static void BindOttoPay()
    {
        if (_payBound)
        {
            return;
        }

        _payBound = true;
        if (!Chainloader.PluginInfos.ContainsKey(OttoPayGuid))
        {
            return;
        }

        Type? api = AccessTools.TypeByName("OttoPay.OttoPayApi");
        _isAuraPayEnabled = api == null ? null : AccessTools.Method(api, "IsAuraPayEnabled");
        _tryWithdraw = api == null ? null : AccessTools.Method(api, "TryWithdraw");
        if (_isAuraPayEnabled == null || _tryWithdraw == null)
        {
            OttoAuraPlugin.OttoAuraLogger.LogWarning("OttoPay is installed, but its AuraPay API was not found. Paid aura repairs are off.");
        }
    }

    internal static string HoverLine(PrivateArea area)
    {
        string what = OttoAuraPlugin.RepairPercentPerTick.Value > 0f && OttoAuraPlugin.HealPerSecond.Value > 0f ? "heals you and repairs your gear"
            : OttoAuraPlugin.RepairPercentPerTick.Value > 0f ? "repairs your gear"
            : OttoAuraPlugin.HealPerSecond.Value > 0f ? "heals you"
            : "is idle";
        string pay = IsPaidRepair && OttoAuraPlugin.RepairPercentPerTick.Value > 0f ? ", paid through AuraPay" : "";
        return $"\n<color=#8FD7FF>Aura {what} within {area.m_radius:0} m{pay}</color>";
    }
}

[HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.GetHoverText))]
static class PrivateAreaGetHoverTextPatch
{
    static void Postfix(PrivateArea __instance, ref string __result)
    {
        if (string.IsNullOrEmpty(__result) || Player.m_localPlayer == null || __instance.m_ownerFaction != Character.Faction.Players)
        {
            return;
        }

        if (__instance.IsEnabled() && __instance.HaveLocalAccess())
        {
            __result += WardAura.HoverLine(__instance);
        }
    }
}
