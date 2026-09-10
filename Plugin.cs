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
using ServerSync;
using UnityEngine;

namespace OttoAura
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    [BepInDependency("org.bepinex.plugins.blacksmithing", BepInDependency.DependencyFlags.SoftDependency)]
    // Loads OttoPay first when both are installed, so AuraPay is ready for the first tick.
    [BepInDependency("potto007.OttoPay", BepInDependency.DependencyFlags.SoftDependency)]
    public class OttoAuraPlugin : BaseUnityPlugin
    {
        internal const string ModName = "OttoAura";
        internal const string ModVersion = "1.0.0";
        internal const string Author = "potto007";
        private const string ModGUID = Author + "." + ModName;
        private static string ConfigFileName = ModGUID + ".cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        internal static string ConnectionError = "";
        private readonly Harmony _harmony = new(ModGUID);

        public static readonly ManualLogSource OttoAuraLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);

        private static readonly ConfigSync ConfigSync = new(ModGUID) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };

        internal static CraftingStation craftingStationClone = null!;
        internal static OttoAuraPlugin context = null!;
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

            HealPerSecond = config("2 - Aura", "Heal Per Second", 1f, new ConfigDescription("Health restored each second to a permitted player inside an active ward. 0 turns healing off.", new AcceptableValueRange<float>(0f, 50f)));
            RepairPercentPerTick = config("2 - Aura", "Repair Percent Per Tick", 5f, new ConfigDescription("Percent of an item's maximum durability restored each tick, for worn gear carried by a permitted player inside an active ward. 0 turns repair off.", new AcceptableValueRange<float>(0f, 100f)));
            CoinsPerItemTick = config("2 - Aura", "Coins Per Item Tick", 1, new ConfigDescription("Coins taken from the OttoPay pouch for each item repaired in a tick. The player must turn AuraPay on in OttoPay. 0 makes repair free, and then OttoPay is not needed.", new AcceptableValueRange<int>(0, 1000)));
            TickSeconds = config("2 - Aura", "Tick Seconds", 1f, new ConfigDescription("Seconds between aura ticks.", new AcceptableValueRange<float>(0.25f, 30f)));
            ShowHealText = config("2 - Aura", "Show Heal Text", Toggle.Off, "If on, each heal tick shows a floating heal number.", false);
            PreventCraftingStationRepair = config("3 - Crafting Stations", "Prevent Crafting Station Repair", Toggle.Off, "If on, players cannot repair items at crafting stations and must use a ward aura.");

            if (Chainloader.PluginInfos.TryGetValue("org.bepinex.plugins.blacksmithing", out var Blacksmithing) && Blacksmithing != null)
            {
                BlacksmithingInstalled = true;
            }

            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }

        private void Update()
        {
            WardAura.Update(Time.deltaTime);
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
                OttoAuraLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                OttoAuraLogger.LogError($"There was an issue loading your {ConfigFileName}");
                OttoAuraLogger.LogError("Please check your config entries for spelling and format!");
            }
        }


        #region ConfigOptions

        private static ConfigEntry<Toggle> _serverConfigLocked = null!;
        internal static ConfigEntry<Toggle> PreventCraftingStationRepair = null!;
        internal static ConfigEntry<float> HealPerSecond = null!;
        internal static ConfigEntry<float> RepairPercentPerTick = null!;
        internal static ConfigEntry<int> CoinsPerItemTick = null!;
        internal static ConfigEntry<float> TickSeconds = null!;
        internal static ConfigEntry<Toggle> ShowHealText = null!;

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
            OttoAuraPlugin.craftingStationClone = ZNetScene.instance.GetPrefab("piece_workbench").GetComponent<CraftingStation>();
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateRepair))]
    static class InventoryGui_InCraftTab_Patch
    {
        static void Postfix(InventoryGui __instance)
        {
            if (OttoAuraPlugin.PreventCraftingStationRepair.Value == OttoAuraPlugin.Toggle.On)
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