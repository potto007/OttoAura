using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace OttoAura.AuraBoost;

// AuraBoost: a Merchant Bank member with AuraPay on drains less stamina running on roads and
// trails. The discount lives in a hook on the status effect chain. The status effect itself only
// carries the icon, the name and the tooltip.
[HarmonyPatch]
internal static class AuraBoostEffect
{
    private const string EffectName = "SE_OttoAura_AuraBoost";
    private const string Flavor = "The magic of AuraPay makes you feel invigorated and lighter on your feet!";

    // How long the icon stays up after the player leaves the road, so a single stride over grass
    // or a pause to jump does not make it blink.
    private const float LingerSeconds = 0.75f;

    private static readonly Assembly GameAssembly = typeof(StatusEffect).Assembly;

    private sealed class AuraBoostStatus : StatusEffect
    {
        public override string GetTooltipString()
        {
            return $"{Flavor}\n\nRun stamina drain: {Percent(OttoAuraPlugin.AuraBoostStaminaTrail.Value)} on trails, wood and metal, {Percent(OttoAuraPlugin.AuraBoostStaminaRoad.Value)} on paved roads and stone.";
        }

        private static string Percent(float fraction) => $"{Mathf.RoundToInt(Mathf.Clamp01(fraction) * 100f)}%";
    }

    private static AuraBoostStatus? _template;
    private static Footing _shownFooting;
    private static float _lingerUntil;

    // The server can switch AuraBoost off, and the player can switch AuraPay off, at any moment.
    internal static bool IsActive => OttoAuraPlugin.AuraBoostEnabled.Value == OttoAuraPlugin.Toggle.On && OttoPayBridge.IsAuraPayEnabled();

    public static void Init()
    {
        _template = ScriptableObject.CreateInstance<AuraBoostStatus>();
        _template.name = EffectName;
        _template.m_name = NameFor(Footing.Trail);
        _template.m_tooltip = Flavor;

        if (ObjectDB.instance)
        {
            AddToObjectDB(ObjectDB.instance);
        }
    }

    public static void Shutdown()
    {
        if (_template == null)
        {
            return;
        }

        if (ObjectDB.instance)
        {
            ObjectDB.instance.m_StatusEffects.Remove(_template);
        }

        Object.Destroy(_template);
        _template = null;
    }

    // ObjectDB is rebuilt on every world load, and it is where a status effect hash resolves.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
    private static void AddAfterObjectDBAwake(ObjectDB __instance)
    {
        AddToObjectDB(__instance);
    }

    private static void AddToObjectDB(ObjectDB db)
    {
        if (_template != null && !db.m_StatusEffects.Contains(_template))
        {
            db.m_StatusEffects.Add(_template);
        }
    }

    private static string NameFor(Footing footing) => footing == Footing.Road ? "AuraBoost (Road)" : "AuraBoost (Trail)";

    private static float UsageFor(Footing footing) => footing switch
    {
        Footing.Trail => OttoAuraPlugin.AuraBoostStaminaTrail.Value,
        Footing.Road => OttoAuraPlugin.AuraBoostStaminaRoad.Value,
        _ => 1f,
    };

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(SEMan), nameof(SEMan.ModifyRunStaminaDrain))]
    private static void RememberIncomingDrain(float drain, out float __state)
    {
        __state = drain;
    }

    // Other mods discount path running through their own status effects. Stacking AuraBoost on
    // top of one would multiply the two, so the bigger discount wins instead. Effects of the
    // game's own types (food, meads, Moder's wind, and item mods that reuse SE_Stats) are ordinary
    // buffs, so they are replayed to find the drain before any modded effect, and still stack.
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(SEMan), nameof(SEMan.ModifyRunStaminaDrain))]
    private static void ApplyDiscount(SEMan __instance, float baseDrain, ref float drain, Vector3 dir, bool minZero, float __state)
    {
        // Player.CheckRun is the only caller that clamps at zero. The other one builds the
        // equipment tooltip, and a road under your feet has nothing to do with your gear.
        if (!minZero || _template == null || __instance.m_character is not Player player || player != Player.m_localPlayer)
        {
            return;
        }

        Footing footing = Ground.Under(player);
        if (footing == Footing.Wild || !IsActive)
        {
            return;
        }

        float gameDrain = __state;
        foreach (StatusEffect effect in __instance.m_statusEffects)
        {
            if (effect.GetType().Assembly == GameAssembly)
            {
                effect.ModifyRunStaminaDrain(baseDrain, ref gameDrain, dir);
            }
        }

        if (gameDrain <= 0f)
        {
            return;
        }

        // Below 1 another mod is already discounting, and only the bigger discount counts. At or
        // above 1 it is doing nothing or charging extra, and AuraBoost applies on top of that.
        float usage = UsageFor(footing);
        float otherMods = drain / gameDrain;
        drain = Mathf.Max(0f, gameDrain * (otherMods < 1f ? Mathf.Min(otherMods, usage) : otherMods * usage));
    }

    // Called from the plugin every frame. Keeps the icon in step with the discount.
    public static void Tick()
    {
        Player? player = Player.m_localPlayer;
        if (_template == null || player == null)
        {
            return;
        }

        SEMan seman = player.GetSEMan();
        bool shown = seman.HaveStatusEffect(_template);

        Footing footing = player.m_running && !player.IsDead() && !player.IsTeleporting() ? Ground.Under(player) : Footing.Wild;
        if (footing != Footing.Wild && IsActive)
        {
            _lingerUntil = Time.time + LingerSeconds;
            if (!shown)
            {
                // AddStatusEffect stores a copy, so the icon choice has to be on the template first.
                _template.m_icon = OttoAuraPlugin.AuraBoostShowStatusIcon.Value == OttoAuraPlugin.Toggle.On ? OttoAuraPlugin.AuraBoostSprite : null;
                _template.m_name = NameFor(footing);
                seman.AddStatusEffect(_template);
                _shownFooting = footing;
            }
            else if (footing != _shownFooting)
            {
                StatusEffect? copy = seman.GetStatusEffect(_template);
                if (copy != null)
                {
                    copy.m_name = NameFor(footing);
                }

                _shownFooting = footing;
            }

            return;
        }

        if (shown && (Time.time >= _lingerUntil || !IsActive))
        {
            seman.RemoveStatusEffect(_template);
        }
    }
}
