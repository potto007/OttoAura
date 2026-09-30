using System;
using System.Globalization;
using System.Text;
using HarmonyLib;

namespace OttoAura;

/// With Blacksmithing installed, a repair that brings gear to full durability also stops it
/// losing durability for a while, longer the better the player's Blacksmithing skill. The
/// deadline and the item's own durability setting ride in the item's custom data, so the hold
/// survives a relog. The keys are the ones the RepairStation mod uses, so gear it repaired
/// keeps its time.
internal static class DurabilityHold
{
    internal const string DeadlineKey = "RepairStation";
    internal const string UseDurabilityKey = "RepairStationUseDurability";

    internal enum State
    {
        None,
        Holding,
        Expired,
    }

    /// Ten minutes at full skill, scaled down with the skill factor, and nothing below half.
    internal static int MinutesFor(float skillFactor)
    {
        return skillFactor >= 0.5f ? (int)(10 * skillFactor) : 0;
    }

    internal static void Start(ItemDrop.ItemData item, float skillFactor, DateTime now)
    {
        item.m_customData[DeadlineKey] = now.AddMinutes(MinutesFor(skillFactor)).ToString(CultureInfo.InvariantCulture);
        item.m_customData[UseDurabilityKey] = item.m_shared.m_useDurability.ToString();
    }

    /// Keeps a hold in force until its deadline. Past it, the item gets its own durability
    /// setting back and both keys go, so the next check finds nothing to do.
    internal static State Refresh(ItemDrop.ItemData? item, DateTime now)
    {
        if (item?.m_shared == null
            || !item.m_customData.TryGetValue(DeadlineKey, out string? deadlineValue)
            || !TryReadDeadline(deadlineValue, out DateTime deadline))
        {
            return State.None;
        }

        if (now <= deadline)
        {
            item.m_shared.m_useDurability = false;
            return State.Holding;
        }

        item.m_customData.Remove(DeadlineKey);
        if (item.m_customData.TryGetValue(UseDurabilityKey, out string? useDurabilityValue) && bool.TryParse(useDurabilityValue, out bool useDurability))
        {
            item.m_shared.m_useDurability = useDurability;
        }

        item.m_customData.Remove(UseDurabilityKey);
        return State.Expired;
    }

    /// Start writes the deadline in the invariant culture. Reading it back in the player's
    /// culture ended every hold at once in a day first locale, so the invariant reading comes
    /// first, and the player's culture is only a fallback for a value written some other way.
    internal static bool TryReadDeadline(string? value, out DateTime deadline)
    {
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out deadline)
            || DateTime.TryParse(value, out deadline);
    }
}

/// Enforces the durability hold on the local player's gear wherever the game asks about it:
/// the item in each hand, and any item checked for being equipped.
[HarmonyPatch]
internal static class BlacksmithingPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.GetRightItem))]
    private static void HumanoidGetRightItemPostfix(Humanoid __instance)
    {
        if (Player.m_localPlayer != null && __instance == Player.m_localPlayer)
        {
            Refresh(__instance.m_rightItem, nameof(Humanoid.GetRightItem));
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.GetLeftItem))]
    private static void HumanoidGetLeftItemPostfix(Humanoid __instance)
    {
        if (Player.m_localPlayer != null && __instance == Player.m_localPlayer)
        {
            Refresh(__instance.m_leftItem, nameof(Humanoid.GetLeftItem));
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.IsItemEquiped))]
    private static void HumanoidIsItemEquipedPrefix(Humanoid __instance, ItemDrop.ItemData item)
    {
        if (__instance.IsPlayer() && __instance == Player.m_localPlayer)
        {
            Refresh(item, nameof(Humanoid.IsItemEquiped));
        }
    }

#if DEBUG
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip), typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool))]
    private static void ItemDataGetTooltipPostfix(ItemDrop.ItemData item, ref string __result)
    {
        if (item.m_dropPrefab == null)
        {
            return;
        }

        string remaining = item.m_customData.TryGetValue(DurabilityHold.DeadlineKey, out string? deadlineValue) && DurabilityHold.TryReadDeadline(deadlineValue, out DateTime deadline)
            ? (deadline - DateTime.Now).ToString()
            : "N/A";
        __result += new StringBuilder("\nRepair Time: ").Append(remaining).Append('\n').ToString();
    }
#endif

    private static void Refresh(ItemDrop.ItemData? item, string caller)
    {
        if (DurabilityHold.Refresh(item, DateTime.Now) == DurabilityHold.State.Expired)
        {
            OttoAuraPlugin.OttoAuraLogger.LogDebug($"({caller}) The durability hold on {item!.m_shared.m_name} ran out.");
        }
    }
}
