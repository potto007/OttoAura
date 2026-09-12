using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;

namespace OttoAura;

// With Blacksmithing installed, a repair also stops durability loss on equipped gear for a
// time that grows with the player's Blacksmithing skill. The repair writes the deadline to
// the item's custom data, and these patches enforce it. Kept from RepairStation, including
// its data keys, so gear repaired by RepairStation keeps its time.

[HarmonyPatch]
static class PlayerItemGetPatch
{
    static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Player), nameof(Player.GetRightItem));
        yield return AccessTools.Method(typeof(Player), nameof(Player.GetLeftItem));
    }

    [HarmonyPostfix]
    static void CheckItem(Player __instance)
    {
        if (Player.m_localPlayer == null) return;
        if (__instance == Player.m_localPlayer)
        {
            // Handle for the right hand and left hand
            if (__instance.m_rightItem is { } rightItem)
            {
                if (rightItem?.m_shared == null) return;
                if (!rightItem.m_customData.TryGetValue("RepairStation", out string? timeValue)) return;
                if (!DateTime.TryParse(timeValue, out DateTime repairTime)) return;
                if (DateTime.Now > repairTime)
                {
                    OttoAuraPlugin.OttoAuraLogger.LogDebug($"(GetRightItem) Removing Time {rightItem.m_shared.m_name}");
                    rightItem.m_customData.Remove("RepairStation");
                    // Set m_useDurability back to what it was
                    if (!rightItem.m_customData.TryGetValue("RepairStationUseDurability", out string? useDurabilityValue)) return;
                    OttoAuraPlugin.OttoAuraLogger.LogDebug($"(GetRightItem) Setting UseDurability {rightItem.m_shared.m_name} {bool.Parse(useDurabilityValue)}");
                    rightItem.m_shared.m_useDurability = bool.Parse(useDurabilityValue);
                    rightItem.m_customData.Remove("RepairStationUseDurability");
                }
                else
                {
                    rightItem.m_shared.m_useDurability = false;
                }
            }

            if (__instance.m_leftItem is { } leftItem)
            {
                if (leftItem?.m_shared == null) return;
                if (!leftItem.m_customData.TryGetValue("RepairStation", out string? timeValue)) return;
                if (!DateTime.TryParse(timeValue, out DateTime repairTime)) return;
                if (DateTime.Now > repairTime)
                {
                    OttoAuraPlugin.OttoAuraLogger.LogDebug($"(GetLeftItem) Removing Time {leftItem.m_shared.m_name}");
                    leftItem.m_customData.Remove("RepairStation");
                    // Set m_useDurability back to what it was
                    if (!leftItem.m_customData.TryGetValue("RepairStationUseDurability", out string? useDurabilityValue)) return;
                    OttoAuraPlugin.OttoAuraLogger.LogDebug($"(GetLeftItem) Setting UseDurability {leftItem.m_shared.m_name} {bool.Parse(useDurabilityValue)}");
                    leftItem.m_shared.m_useDurability = bool.Parse(useDurabilityValue);
                    leftItem.m_customData.Remove("RepairStationUseDurability");
                }
                else
                {
                    leftItem.m_shared.m_useDurability = false;
                }
            }
        }
    }
}

[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.IsItemEquiped))]
static class ItemDropItemDataIsItemEquipedPatch
{
    static void Prefix(Humanoid __instance, ItemDrop.ItemData item)
    {
        if (!__instance.IsPlayer() || __instance != Player.m_localPlayer) return;
        if (item?.m_shared == null) return;
        if (!item.m_customData.TryGetValue("RepairStation", out string? timeValue)) return;
        if (!DateTime.TryParse(timeValue, out DateTime repairTime)) return;
        if (DateTime.Now > repairTime)
        {
            OttoAuraPlugin.OttoAuraLogger.LogDebug($"(IsItemEquiped) Removing Time {item.m_shared.m_name}");
            item.m_customData.Remove("RepairStation");
            // Set m_useDurability back to what it was
            if (!item.m_customData.TryGetValue("RepairStationUseDurability", out string? useDurabilityValue)) return;
            OttoAuraPlugin.OttoAuraLogger.LogDebug($"(IsItemEquiped) Setting UseDurability {item.m_shared.m_name} {bool.Parse(useDurabilityValue)}");
            item.m_shared.m_useDurability = bool.Parse(useDurabilityValue);
            item.m_customData.Remove("RepairStationUseDurability");
        }
        else
        {
            item.m_shared.m_useDurability = false;
        }
    }
}

#if DEBUG
[HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip), typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool))]
static class ItemDropItemDataGetTooltipPatch
{
    static void Postfix(ItemDrop.ItemData item, ref string __result)
    {
        if (item.m_dropPrefab is { } prefab)
        {
            StringBuilder sb = new("\n");
            // Add the Repair Time compared to the current time
            var value = item.m_customData.TryGetValue("RepairStation", out string? timeValue) ? timeValue : "N/A";
            // Tell me how many minutes left until Datetime.Now is greater than the repair time
            sb.Append("Repair Time: ").Append(DateTime.TryParse(value, out DateTime repairTime) ? (repairTime - DateTime.Now).ToString() : "N/A").Append("\n");
            __result += sb.ToString();
        }
    }
}
#endif