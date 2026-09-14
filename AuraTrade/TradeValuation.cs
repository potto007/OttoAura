using OttoAura.AuraMove;

namespace OttoAura.AuraTrade;

// TradeValuation: what counts as a valuable, what it is worth, and what the AuraPay network keeps.
//
// The fee works like a card processor's: a flat charge per trade plus a percent of the gross. The
// flat part is paid once however much changes hands, so one big trade keeps more coins than the
// same valuables sold a few at a time.
internal static class TradeValuation
{
    // Coins have a value of 1 and would otherwise count as a valuable. OttoPay treats only the
    // vanilla coin as currency.
    private const string CoinsName = "$item_coins";

    internal readonly struct Quote
    {
        internal readonly int Gross;
        internal readonly int Fee;

        internal Quote(int gross, int fee)
        {
            Gross = gross;
            Fee = fee;
        }

        internal int Net => Gross - Fee;

        // A trade that would credit nothing is refused rather than taking the valuables for free.
        internal bool Tradeable => Net > 0;
    }

    internal static bool IsValuable(ItemDrop.ItemData? item)
    {
        if (item == null || item.m_shared.m_value <= 0 || item.m_shared.m_name == CoinsName)
        {
            return false;
        }

        string prefab = item.m_dropPrefab != null ? item.m_dropPrefab.name : "";
        return !MoveEligibility.IsListed(OttoAuraPlugin.AuraTradeDeniedItems.Value, prefab);
    }

    internal static long Worth(ItemDrop.ItemData item, int stack) => (long)item.m_shared.m_value * stack;

    // A gross that does not fit an int quotes as nothing, so it can never be traded. OttoPay's
    // balance is an int too.
    internal static Quote QuoteFor(long gross)
    {
        if (gross <= 0 || gross > int.MaxValue)
        {
            return new Quote(0, 0);
        }

        // Percent is held as whole basis points and rounded up in integers: a float percent such
        // as 2.9 times 1000 lands a hair above 29 and would round up to 30.
        long basisPoints = (long)System.Math.Round(System.Math.Max(0f, OttoAuraPlugin.AuraTradePercentFee.Value) * 100.0);
        long percentPart = (gross * basisPoints + 9999) / 10000;
        long fee = System.Math.Max(0, OttoAuraPlugin.AuraTradeFlatFee.Value) + percentPart;
        return new Quote((int)gross, (int)System.Math.Min(fee, gross));
    }
}
