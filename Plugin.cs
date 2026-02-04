using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using PieceManager;
using ServerSync;
using UnityEngine;

namespace RepairStation
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    [BepInDependency("org.bepinex.plugins.blacksmithing", BepInDependency.DependencyFlags.SoftDependency)]
    public class RepairStationPlugin : BaseUnityPlugin
    {
        internal const string ModName = "RepairStation";
        internal const string ModVersion = "1.2.6";
        internal const string Author = "Azumatt";
        private const string ModGUID = Author + "." + ModName;
        private static string ConfigFileName = ModGUID + ".cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        internal static string ConnectionError = "";
        private readonly Harmony _harmony = new(ModGUID);

        public static readonly ManualLogSource RepairStationLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);

        private static readonly ConfigSync ConfigSync = new(ModGUID) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };

        internal static CraftingStation craftingStationClone = null!;
        internal static RepairStationPlugin context = null!;
        internal static bool BlacksmithingInstalled;

        public enum Toggle
        {
            On = 1,
            Off = 0
        }

        public void Awake()
        {
            context = this;
            _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On, "If on, the configuration is locked and can be changed by server admins only.");
            _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);
            
            PreventCraftingStationRepair = config("2 - Repair Station", "Prevent Crafting Station Repair", Toggle.Off, "If on, Players will not be able to repair items at crafting stations. They must use the Repair Station.");
            
            RepairAllItems = config("2 - Repair Station Cost", "Repair All Items", Toggle.Off, "If set to true, the RepairItems() method will be called in a loop until all repairable items are repaired. If set to false, the RepairItems() method will be called once.");
            UseItemMultiplier = config("2 - Repair Station Cost", "Use Item Multiplier", Toggle.On, "If set to true, the Cost Item Amount times the amount of items needing repair will be used to calculate the cost of repairing an item. If set to false, the Cost Item Amount will be used to calculate the cost of repairing an item.");
            ShouldCost = config("2 - Repair Station Cost", "Should Cost?", Toggle.Off, "Should using the repair station cost the player something from their inventory?");
            RepairItem = config("2 - Repair Station Cost", "Cost Item", "Coins", "Item needed to use the Repair Station. Limit is 1 item: Goes by prefab name and must be a valid item the player can hold. List of vanilla items here: https://valheim-modding.github.io/Jotunn/data/objects/item-list.html");
            Cost = config("2 - Repair Station Cost", "Cost Item Amount", 5, "Amount of the item needed to repair all items in the inventory.");
            
            
            BuildPiece repairStation = new("repairstation", "RepairStation");
            repairStation.Name.English("Repair Station");
            repairStation.Description.English("Simple station to repair your tools. All at once. Just interact with this shit.");
            repairStation.RequiredItems.Add("Iron", 30, true);
            repairStation.RequiredItems.Add("Wood", 10, true);
            repairStation.RequiredItems.Add("SurtlingCore", 3, true);
            repairStation.Category.Set(BuildPieceCategory.Misc);
            repairStation.Crafting.Set(CraftingTable.Forge);

            repairStation.Prefab.AddComponent<RepairStation>();
            MaterialReplacer.RegisterGameObjectForMatSwap(repairStation.Prefab);

            if (Chainloader.PluginInfos.TryGetValue("org.bepinex.plugins.blacksmithing", out var Blacksmithing))
            {
                if (Blacksmithing != null)
                {
                    BlacksmithingInstalled = true;
                }
            }
            
            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }
        

        internal static void AutoDoc()
        {
#if DEBUG

            // Store Regex to get all characters after a [
            Regex regex = new(@"\[(.*?)\]");

            // Strip using the regex above from Config[x].Description.Description
            string Strip(string x) => regex.Match(x).Groups[1].Value;
            StringBuilder sb = new();
            string lastSection = "";
            foreach (ConfigDefinition x in context.Config.Keys)
            {
                // skip first line
                if (x.Section != lastSection)
                {
                    lastSection = x.Section;
                    sb.Append($"{Environment.NewLine}`{x.Section}`{Environment.NewLine}");
                }
                sb.Append($"\n{x.Key} [{Strip(context.Config[x].Description.Description)}]" +
                          $"{Environment.NewLine}   * {context.Config[x].Description.Description.Replace("[Synced with Server]", "").Replace("[Not Synced with Server]", "")}" +
                          $"{Environment.NewLine}     * Default Value: {context.Config[x].GetSerializedValue()}{Environment.NewLine}");
            }
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, $"{ModName}_AutoDoc.md"), sb.ToString());
#endif
        }

        private void OnDestroy()
        {
            Config.Save();
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                RepairStationLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                RepairStationLogger.LogError($"There was an issue loading your {ConfigFileName}");
                RepairStationLogger.LogError("Please check your config entries for spelling and format!");
            }
        }


        #region ConfigOptions

        private static ConfigEntry<Toggle> _serverConfigLocked = null!;
        internal static ConfigEntry<Toggle> PreventCraftingStationRepair = null!;
        internal static ConfigEntry<Toggle> RepairAllItems = null!;
        internal static ConfigEntry<Toggle> UseItemMultiplier = null!;
        internal static ConfigEntry<Toggle> ShouldCost = null!;
        internal static ConfigEntry<int> Cost = null!;
        internal static ConfigEntry<string> RepairItem = null!;

        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description, bool synchronizedSetting = true)
        {
            ConfigDescription extendedDescription = new(description.Description + (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"), description.AcceptableValues, description.Tags);
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);
            //var configEntry = Config.Bind(group, name, value, description);

            SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, string description, bool synchronizedSetting = true)
        {
            return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
        }

        private class ConfigurationManagerAttributes
        {
            [UsedImplicitly] public int? Order = null!;
            [UsedImplicitly] public bool? Browsable = null!;
            [UsedImplicitly] public string? Category = null!;
            [UsedImplicitly] public Action<ConfigEntryBase>? CustomDrawer = null!;
        }

        #endregion
    }
    
    [HarmonyPatch(typeof(ZNetScene),nameof(ZNetScene.Awake))]
    static class ZNetScene_Awake_Patch
    {
        static void Postfix(ZNetScene __instance)
        {
            RepairStationPlugin.craftingStationClone = ZNetScene.instance.GetPrefab("piece_workbench").GetComponent<CraftingStation>();
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateRepair))]
    static class InventoryGui_InCraftTab_Patch
    {
        static void Postfix(InventoryGui __instance)
        {
            if (RepairStationPlugin.PreventCraftingStationRepair.Value == RepairStationPlugin.Toggle.On)
            {
                if (Player.m_localPlayer.GetCurrentCraftingStation() != null && !Player.m_localPlayer.NoCostCheat())
                {
                    __instance.m_repairPanel.gameObject.SetActive(false);
                    __instance.m_repairPanelSelection.gameObject.SetActive(false);
                    __instance.m_repairButton.gameObject.SetActive(false);
                }
            }
        }
    }
}