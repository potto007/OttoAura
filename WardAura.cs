using System;
using System.Collections.Generic;
using HarmonyLib;
using SkillManager;
using UnityEngine;

namespace OttoAura;

/// The ward aura heals the local player and repairs their worn gear. Health, durability and
/// the bank balance all belong to the player running this client, so each client ticks for
/// itself and no RPC is needed.
internal static class WardAura
{
    private const float EmptyBalanceMessageSeconds = 30f;

    private static readonly List<ItemDrop.ItemData> _wornItems = new();
    private static readonly List<ItemDrop.ItemData> _repairedItems = new();
    private static float _elapsed;
    private static float _lastEmptyBalanceMessage = -1000f;

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

    /// An aura needs a player ward that is switched on, contains the player, and lists the
    /// player as its creator or as permitted. That is the same access a ward grants to build.
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

    /// Fills worn with the inventory's items that have lost durability and can be repaired.
    /// Vanilla's worn list also holds items that can never be repaired, such as torches.
    internal static void CollectRepairable(Inventory inventory, List<ItemDrop.ItemData> worn)
    {
        worn.Clear();
        inventory.GetWornItems(worn);
        worn.RemoveAll(item => !item.m_shared.m_canBeReparied);
    }

    /// Puts percent of each item's maximum durability back, never past the maximum, and lists
    /// the items that reached it so the caller can show the repaired message for each one.
    internal static void RepairStep(List<ItemDrop.ItemData> worn, float percent, List<ItemDrop.ItemData> repaired)
    {
        repaired.Clear();
        foreach (ItemDrop.ItemData item in worn)
        {
            float max = item.GetMaxDurability();
            item.m_durability = Mathf.Min(max, item.m_durability + max * percent / 100f);
            if (item.m_durability >= max)
            {
                repaired.Add(item);
            }
        }
    }

    internal static string HoverLine(PrivateArea area)
    {
        return HoverLine(OttoAuraPlugin.RepairPercentPerTick.Value, OttoAuraPlugin.HealPerSecond.Value, OttoAuraPlugin.CoinsPerItemTick.Value, area.m_radius);
    }

    /// The ward's hover line on plain values, so the wording can be checked without the engine.
    /// A paid repair is only worth mentioning while repair is on at all.
    internal static string HoverLine(float repairPercent, float healPerSecond, int coinsPerItem, float radius)
    {
        bool repairs = repairPercent > 0f;
        bool heals = healPerSecond > 0f;
        string what = repairs && heals ? "heals you and repairs your gear"
            : repairs ? "repairs your gear"
            : heals ? "heals you"
            : "is idle";
        string pay = repairs && coinsPerItem > 0 ? ", paid through AuraPay" : "";
        return $"\n<color=#8FD7FF>Aura {what} within {radius:0} m{pay}</color>";
    }

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

        CollectRepairable(player.GetInventory(), _wornItems);
        if (_wornItems.Count == 0)
        {
            return;
        }

        if (IsPaidRepair && !TryPay(player, OttoAuraPlugin.CoinsPerItemTick.Value * _wornItems.Count))
        {
            return;
        }

        RepairStep(_wornItems, percent, _repairedItems);
        foreach (ItemDrop.ItemData item in _repairedItems)
        {
            OnFullyRepaired(player, item);
        }
    }

    private static void OnFullyRepaired(Player player, ItemDrop.ItemData item)
    {
        if (OttoAuraPlugin.BlacksmithingInstalled)
        {
            DurabilityHold.Start(item, player.GetSkillFactor(Skill.fromName("Blacksmithing")), DateTime.Now);
        }

        player.Message(MessageHud.MessageType.TopLeft, Localization.instance.Localize("$msg_repaired", item.m_shared.m_name));
        CraftingStation? workbench = CraftingStationPatches.Workbench;
        if (workbench != null)
        {
            workbench.m_repairItemDoneEffects.Create(player.transform.position, Quaternion.identity);
        }
    }

    /// Repair is all or nothing for the tick: either every worn item is paid for, or none is
    /// repaired. The empty balance message repeats at most every half minute.
    private static bool TryPay(Player player, int cost)
    {
        if (!OttoPayBridge.IsAuraPayEnabled())
        {
            return false;
        }

        if (OttoPayBridge.TryWithdraw(cost))
        {
            return true;
        }

        if (Time.time - _lastEmptyBalanceMessage > EmptyBalanceMessageSeconds)
        {
            player.Message(MessageHud.MessageType.TopLeft, "Your Merchant Bank balance is empty. The aura cannot repair your gear.");
            _lastEmptyBalanceMessage = Time.time;
        }

        return false;
    }
}

/// Tells a player pointing at a ward they may use what its aura does, and what AuraTrade
/// would pay there.
[HarmonyPatch]
internal static class WardAuraPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.GetHoverText))]
    private static void PrivateAreaGetHoverTextPostfix(PrivateArea __instance, ref string __result)
    {
        if (string.IsNullOrEmpty(__result) || Player.m_localPlayer == null || __instance.m_ownerFaction != Character.Faction.Players)
        {
            return;
        }

        if (__instance.IsEnabled() && __instance.HaveLocalAccess())
        {
            __result += WardAura.HoverLine(__instance);
            __result += AuraTrade.AuraTradeController.HoverLine(__instance);
        }
    }
}
