using System.Text;
using HarmonyLib;

namespace OttoAura.AuraTrade;

// TradeTooltip: every valuable's tooltip says what the Merchant Guild would pay for it, one item
// and, for a stack, the whole stack: gross worth, the AuraPay fee, and the net that reaches the
// balance. Showing both side by side is what makes the flat part of the fee visible.
internal static class TradeTooltip
{
    private const string ValueTag = "\n$item_value:";

    internal static string Insert(string tooltip, ItemDrop.ItemData item, int stack)
    {
        StringBuilder lines = new();
        if (stack > 1)
        {
            AppendQuote(lines, "AuraTrade each", TradeValuation.QuoteFor(item.m_shared.m_value));
            AppendQuote(lines, $"AuraTrade stack of {stack}", TradeValuation.QuoteFor(TradeValuation.Worth(item, stack)));
        }
        else
        {
            AppendQuote(lines, "AuraTrade", TradeValuation.QuoteFor(item.m_shared.m_value));
        }

        // Right under vanilla's value line, where the eye already is. Appended at the end when a
        // mod or a game update has moved that line.
        int valueLine = tooltip.IndexOf(ValueTag, System.StringComparison.Ordinal);
        if (valueLine < 0)
        {
            return tooltip + lines;
        }

        int lineEnd = tooltip.IndexOf('\n', valueLine + 1);
        return lineEnd < 0 ? tooltip + lines : tooltip.Insert(lineEnd, lines.ToString());
    }

    private static void AppendQuote(StringBuilder lines, string label, TradeValuation.Quote quote)
    {
        lines.Append(quote.Tradeable
            ? $"\n{label}: <color=orange>{quote.Gross}</color> worth, <color=orange>{quote.Fee}</color> fee, <color=orange>{quote.Net}</color> net"
            : $"\n{label}: <color=orange>{quote.Gross}</color> worth, the <color=orange>{quote.Fee}</color> fee takes it all");
    }
}

// The static overload is the one every tooltip goes through, the instance GetTooltip included.
// Shown whether or not AuraPay is on, so a player can see what joining the bank would pay.
[HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip),
    typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool))]
static class ItemDataGetTooltipPatch
{
    static void Postfix(ItemDrop.ItemData item, bool crafting, int stackOverride, bool appending, ref string __result)
    {
        if (crafting
            || appending
            || OttoAuraPlugin.AuraTradeEnabled.Value == OttoAuraPlugin.Toggle.Off
            || !OttoPayBridge.CanDeposit
            || !TradeValuation.IsValuable(item))
        {
            return;
        }

        __result = TradeTooltip.Insert(__result, item, stackOverride > 0 ? stackOverride : item.m_stack);
    }
}
