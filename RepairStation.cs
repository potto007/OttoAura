using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using SkillManager;
using UnityEngine;
using Object = System.Object;

namespace OttoAura;

public class RepairStation : MonoBehaviour, Hoverable, Interactable
{
    internal static string? ItemName;
    public List<ItemDrop.ItemData> m_tempWornItemsHover = new();
    internal static int CostAmount;

    public string GetHoverText()
    {
        GetWornItemsHover(m_tempWornItemsHover);
        CostAmount = OttoAuraPlugin.UseItemMultiplier.Value == OttoAuraPlugin.Toggle.On && OttoAuraPlugin.RepairAllItems.Value == OttoAuraPlugin.Toggle.On ? OttoAuraPlugin.Cost.Value * m_tempWornItemsHover.Count : OttoAuraPlugin.Cost.Value;
        StringBuilder stringBuilder = new();
        stringBuilder.Append(Localization.instance.Localize($"{GetHoverName()}{Environment.NewLine}Press [<color=yellow><b>$KEY_Use</b></color>] to repair everything in your inventory{(OttoAuraPlugin.ShouldCost.Value == OttoAuraPlugin.Toggle.On ? $" (Uses {CostAmount} {OttoAuraPlugin.RepairItem.Value})" : "")}"));

        return stringBuilder.ToString();
    }

    public string GetHoverName() => Localization.instance.Localize("$piece_repairstation");

    // Valheim 1.0 added this to Hoverable. Zero keeps the vanilla hover position.
    public float GetHoverOffset() => 0f;


    public bool Interact(Humanoid character, bool hold, bool alt)
    {
        if (hold)
            return false;
        if (!CheckRepairCost(costValue: CostAmount))
        {
            ItemDrop? item = ZNetScene.instance.GetPrefab(OttoAuraPlugin.RepairItem.Value).GetComponent<ItemDrop>();
            if (!item) return false;
            var amountPlayerHas = Player.m_localPlayer.GetInventory().CountItems(item.m_itemData.m_shared.m_name);
            var costAmountHeld = CostAmount;
            Player.m_localPlayer.Message(MessageHud.MessageType.Center, $"Required Items Needed : {costAmountHeld - amountPlayerHas} {ItemName}");
            return false;
        }

        if (!HaveRepairableItems())
        {
            Player.m_localPlayer.Message(MessageHud.MessageType.Center, "No items to repair");
        }
        else
        {
            int repairCount = 0;
            if (OttoAuraPlugin.RepairAllItems.Value == OttoAuraPlugin.Toggle.On)
            {
                while (HaveRepairableItems())
                {
                    RepairItems();
                    ++repairCount;
                }
            }
            else
            {
                RepairItems();
                ++repairCount;
            }

            if (repairCount <= 0) return true;
            var costValue = OttoAuraPlugin.UseItemMultiplier.Value == OttoAuraPlugin.Toggle.On ? OttoAuraPlugin.Cost.Value * repairCount : OttoAuraPlugin.Cost.Value;
            CheckRepairCost(true, costValue);
            OttoAuraPlugin.craftingStationClone.m_repairItemDoneEffects.Create(transform.position, Quaternion.identity);
        }


        return true;
    }

    public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;

    public bool HaveRepairableItems()
    {
        if (Player.m_localPlayer == null) return false;
        InventoryGui.instance.m_tempWornItems.Clear();
        Player.m_localPlayer.GetInventory().GetWornItems(InventoryGui.instance.m_tempWornItems);
        foreach (ItemDrop.ItemData tempWornItem in InventoryGui.instance.m_tempWornItems)
        {
            if (CanRepairItems(tempWornItem))
                return true;
        }

        return false;
    }

    public bool CanRepairItems(ItemDrop.ItemData item)
    {
        if (Player.m_localPlayer == null || !item.m_shared.m_canBeReparied)
            return false;
        if (Player.m_localPlayer.NoCostCheat())
            return true;
        // TODO: Add a gating system for this
        //Recipe recipe = ObjectDB.instance.GetRecipe(item);
        // return !(recipe == null) && (!(recipe.m_craftingStation == null) || !(recipe.m_repairStation == null)) && (recipe.m_repairStation != null && recipe.m_repairStation.m_name == currentCraftingStation.m_name || (Object) recipe.m_craftingStation != null && recipe.m_craftingStation.m_name == currentCraftingStation.m_name) && currentCraftingStation.GetLevel() >= recipe.m_minStationLevel;
        return true;
    }

    public void RepairItems()
    {
        if (Player.m_localPlayer == null)
            return;
        InventoryGui.instance.m_tempWornItems.Clear();
        m_tempWornItemsHover.Clear();
        Player.m_localPlayer.GetInventory().GetWornItems(InventoryGui.instance.m_tempWornItems);
        //CraftingStation craftingStationClone = null;

        foreach (ItemDrop.ItemData tempWornItem in InventoryGui.instance.m_tempWornItems)
        {
            if (CanRepairItems(tempWornItem))
            {
                if (OttoAuraPlugin.BlacksmithingInstalled)
                {
                    int minutesToSet = 0;
                    float skillFactor = Player.m_localPlayer.GetSkillFactor(Skill.fromName("Blacksmithing"));
                    if (skillFactor >= 0.5f)
                        minutesToSet = (int)(10 * skillFactor);
                    tempWornItem.m_customData["RepairStation"] = DateTime.Now.AddMinutes(minutesToSet).ToString(CultureInfo.InvariantCulture);
                    OttoAuraPlugin.OttoAuraLogger.LogDebug($"(Can Repair Items) Setting Time {tempWornItem.m_shared.m_name} {DateTime.Now.AddMinutes(minutesToSet)}");
                    tempWornItem.m_durability = tempWornItem.GetMaxDurability();
                    // Cache the m_useDurability into custom data
                    tempWornItem.m_customData["RepairStationUseDurability"] = tempWornItem.m_shared.m_useDurability.ToString();
                    OttoAuraPlugin.OttoAuraLogger.LogDebug($"(Can Repair Items) Storing UseDurability {tempWornItem.m_shared.m_name} {tempWornItem.m_shared.m_useDurability}");
                }
                else
                {
                    tempWornItem.m_durability = tempWornItem.GetMaxDurability();
                }

                if (OttoAuraPlugin.craftingStationClone != null)
                    OttoAuraPlugin.craftingStationClone.m_repairItemDoneEffects.Create(transform.position, Quaternion.identity);
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_repaired", tempWornItem.m_shared.m_name));
                return;
            }
        }

        Player.m_localPlayer.Message(MessageHud.MessageType.Center, "No more items to repair");
    }

    internal static bool CheckRepairCost(bool shouldRemove = false, int costValue = 1)
    {
        if (OttoAuraPlugin.ShouldCost.Value == OttoAuraPlugin.Toggle.Off) return true;
        ItemDrop? item = ZNetScene.instance.GetPrefab(OttoAuraPlugin.RepairItem.Value).GetComponent<ItemDrop>();
        if (!item) return false;
        ItemName = Localization.instance.Localize(item.m_itemData.m_shared.m_name);
        if (Player.m_localPlayer.GetInventory().CountItems(item.m_itemData.m_shared.m_name) >= costValue)
        {
            if (shouldRemove)
            {
                OttoAuraPlugin.OttoAuraLogger.LogError($"Removing Items {costValue}");
                Player.m_localPlayer.GetInventory().RemoveItem(item.m_itemData.m_shared.m_name, costValue);
                Player.m_localPlayer.ShowRemovedMessage(item.m_itemData, costValue);
            }

            return true;
        }

        return false;
    }

    public void GetWornItemsHover(List<ItemDrop.ItemData> worn)
    {
        foreach (ItemDrop.ItemData itemData in Player.m_localPlayer.GetInventory().GetAllItems())
        {
            if (!itemData.m_shared.m_useDurability || !(itemData.m_durability < (double)itemData.GetMaxDurability())) continue;
            if (worn.Contains(itemData)) continue;
            if (CanRepairItems(itemData))
                worn.Add(itemData);
        }
    }
}

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
[HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip), typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float))]
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