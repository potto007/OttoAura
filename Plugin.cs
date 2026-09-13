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
using OttoAura.AuraBoost;
using OttoAura.AuraMove;
using ServerSync;
using UnityEngine;

namespace OttoAura
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    [BepInDependency("org.bepinex.plugins.blacksmithing", BepInDependency.DependencyFlags.SoftDependency)]
    // Repairs and AuraBoost both run through AuraPay, so OttoAura does not load without OttoPay.
    [BepInDependency("potto007.OttoPay", "1.4.0")]
    public class OttoAuraPlugin : BaseUnityPlugin
    {
        internal const string ModName = "OttoAura";
        internal const string ModVersion = "1.2.0";
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
            CoinsPerItemTick = config("2 - Aura", "Coins Per Item Tick", 1, new ConfigDescription("Coins charged to the OttoPay Merchant Bank balance for each item repaired in a tick. The player must turn AuraPay on in OttoPay. 0 makes repair free.", new AcceptableValueRange<int>(0, 1000)));
            TickSeconds = config("2 - Aura", "Tick Seconds", 1f, new ConfigDescription("Seconds between aura ticks.", new AcceptableValueRange<float>(0.25f, 30f)));
            ShowHealText = config("2 - Aura", "Show Heal Text", Toggle.Off, "If on, each heal tick shows a floating heal number.", false);
            PreventCraftingStationRepair = config("3 - Crafting Stations", "Prevent Crafting Station Repair", Toggle.Off, "If on, players cannot repair items at crafting stations and must use a ward aura.");
            AuraBoostEnabled = config("4 - AuraBoost", "Enabled", Toggle.On, "If on, Merchant Bank members with AuraPay on in OttoPay drain less stamina while running on roads and trails.");
            AuraBoostStaminaTrail = config("4 - AuraBoost", "Stamina Usage Trail", 0.5f, new ConfigDescription("Run stamina drain on dirt paths, wood and metal, as a fraction of vanilla. 1 is vanilla, 0 is no drain.", new AcceptableValueRange<float>(0f, 1f)));
            AuraBoostStaminaRoad = config("4 - AuraBoost", "Stamina Usage Road", 0f, new ConfigDescription("Run stamina drain on paved roads and stone, as a fraction of vanilla. 1 is vanilla, 0 is no drain.", new AcceptableValueRange<float>(0f, 1f)));
            AuraBoostShowStatusIcon = config("4 - AuraBoost", "Show Status Icon", Toggle.On, "If on, the AuraBoost icon shows in the status bar while the effect is active.");
            AuraBoostSprite = LoadSprite("auraboost_icon.png");

            AuraMoveEnabled = config("5 - AuraMove", "Enabled", Toggle.On, "If on, the Merchant Guild will move placed objects for a fee while AuraPay is active.");
            AuraMoveCoins = config("5 - AuraMove", "Coins", 5, new ConfigDescription("Coins charged to the Merchant Bank balance per completed move. 0 makes moving free, but AuraPay must still be on.", new AcceptableValueRange<int>(0, 1000)));
            AuraMoveMaxDistance = config("5 - AuraMove", "Max Move Distance", 10f, new ConfigDescription("How far in metres the destination may sit from where the object stands now.", new AcceptableValueRange<float>(1f, 64f)));
            AuraMoveSupportImmovable = config("5 - AuraMove", "Support Is Immovable", Toggle.On, "If on, non-furniture pieces that carry structural load cannot be moved.");
            AuraMoveAllowedPrefabs = config("5 - AuraMove", "Allowed Prefabs", "wood_fine_stack,blackwood_stack,bone_stack,piece_beehive", "Comma-separated prefab names that skip every eligibility restriction and can always be moved.");
            AuraMoveDeniedPrefabs = config("5 - AuraMove", "Denied Prefabs", "fire_pit,bonfire,hearth,windmill", "Comma-separated prefab names that can never be moved.");
            AuraMoveShimmerSeconds = config("5 - AuraMove", "Shimmer Seconds", 0.6f, new ConfigDescription("Total duration of the shrink and grow animation. 0 snaps and only plays the burst effects.", new AcceptableValueRange<float>(0f, 3f)));
            AuraMoveEffectPrefabs = config("5 - AuraMove", "Effect Prefabs", "vfx_Place_wood_pole,sfx_build_cultivator", "Comma-separated fallback effect prefabs used when the moved piece has no place effect of its own.");
            AuraMoveKey = config("5 - AuraMove", "Move Key", new KeyboardShortcut(KeyCode.V, KeyCode.LeftAlt), "Keyboard shortcut to grab and confirm a move.", false);
            AuraMoveGamepadModifier = config("5 - AuraMove", "Gamepad Modifier", "JoyAltKeys", "ZInput button that must be held with the gamepad button. Leave empty for no modifier.", false);
            AuraMoveGamepadButton = config("5 - AuraMove", "Gamepad Button", "JoyButtonY", "ZInput button that grabs and confirms a move.", false);

            if (Chainloader.PluginInfos.TryGetValue("org.bepinex.plugins.blacksmithing", out var Blacksmithing) && Blacksmithing != null)
            {
                BlacksmithingInstalled = true;
            }

            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }

        public void Start()
        {
            AuraBoostEffect.Init();
            AuraMoveController.Init();
        }

        private void Update()
        {
            WardAura.Update(Time.deltaTime);
            AuraBoostEffect.Tick();
            AuraMoveController.Tick();
        }

        // UnityEngine.ImageConversionModule cannot be referenced from net48: its metadata
        // names ReadOnlySpan<byte>, which lives in the game's Mono mscorlib and not in the
        // net48 reference assemblies. The byte[] overload of LoadImage still exists, so it is
        // bound at runtime instead.
        private static Sprite? LoadSprite(string name)
        {
            MethodInfo? loadImage = AccessTools.Method("UnityEngine.ImageConversion:LoadImage", new[] { typeof(Texture2D), typeof(byte[]) });
            Assembly assembly = Assembly.GetExecutingAssembly();
            using Stream? resource = assembly.GetManifestResourceStream($"{assembly.GetName().Name}.assets.{name}");
            if (loadImage == null || resource == null)
            {
                OttoAuraLogger.LogError($"Could not load the {name} icon.");
                return null;
            }

            using MemoryStream bytes = new();
            resource.CopyTo(bytes);
            Texture2D texture = new(0, 0);
            loadImage.Invoke(null, new object[] { texture, bytes.ToArray() });
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);
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
            AuraMoveController.Shutdown();
            AuraBoostEffect.Shutdown();
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
        internal static ConfigEntry<Toggle> AuraBoostEnabled = null!;
        internal static ConfigEntry<float> AuraBoostStaminaTrail = null!;
        internal static ConfigEntry<float> AuraBoostStaminaRoad = null!;
        internal static ConfigEntry<Toggle> AuraBoostShowStatusIcon = null!;
        internal static Sprite? AuraBoostSprite;
        internal static ConfigEntry<Toggle> AuraMoveEnabled = null!;
        internal static ConfigEntry<int> AuraMoveCoins = null!;
        internal static ConfigEntry<float> AuraMoveMaxDistance = null!;
        internal static ConfigEntry<Toggle> AuraMoveSupportImmovable = null!;
        internal static ConfigEntry<string> AuraMoveAllowedPrefabs = null!;
        internal static ConfigEntry<string> AuraMoveDeniedPrefabs = null!;
        internal static ConfigEntry<float> AuraMoveShimmerSeconds = null!;
        internal static ConfigEntry<string> AuraMoveEffectPrefabs = null!;
        internal static ConfigEntry<KeyboardShortcut> AuraMoveKey = null!;
        internal static ConfigEntry<string> AuraMoveGamepadModifier = null!;
        internal static ConfigEntry<string> AuraMoveGamepadButton = null!;

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