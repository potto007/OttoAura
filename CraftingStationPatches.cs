using HarmonyLib;
using UnityEngine;

namespace OttoAura;

/// The crafting station side of repair. The aura borrows the workbench's repaired effect, and
/// a server that wants every repair to go through a ward can hide the station's repair panel.
[HarmonyPatch]
internal static class CraftingStationPatches
{
    internal static CraftingStation? Workbench;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
    private static void ZNetSceneAwakePostfix(ZNetScene __instance)
    {
        GameObject? prefab = __instance.GetPrefab("piece_workbench");
        Workbench = prefab == null ? null : prefab.GetComponent<CraftingStation>();
    }

    /// The no cost cheat keeps station repair, so an admin can still fix gear anywhere.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateRepair))]
    private static void InventoryGuiUpdateRepairPostfix(InventoryGui __instance)
    {
        Player? player = Player.m_localPlayer;
        if (OttoAuraPlugin.PreventCraftingStationRepair.Value == OttoAuraPlugin.Toggle.Off
            || player == null
            || player.GetCurrentCraftingStation() == null
            || player.NoCostCheat())
        {
            return;
        }

        __instance.m_repairPanel.gameObject.SetActive(false);
        __instance.m_repairPanelSelection.gameObject.SetActive(false);
        __instance.m_repairButton.gameObject.SetActive(false);
    }
}
