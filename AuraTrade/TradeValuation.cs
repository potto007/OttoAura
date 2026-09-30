using OttoAura.AuraMove;

namespace OttoAura.AuraTrade;

/// The AuraPay network's cut of a trade. It works like a card processor's: a flat charge per
/// trade plus a percent of the gross. The flat part is paid once however much changes hands,
/// so one big trade keeps more coins than the same valuables sold a few at a time.
internal readonly struct TradeFees
{
    internal readonly int Flat;
    internal readonly float Percent;

    internal TradeFees(int flat, float percent)
    {
        Flat = flat;
        Percent = percent;
    }
}

/// What counts as a valuable, what it is worth, and what the AuraPay network keeps.
internal static class TradeValuation
{
    /// Coins have a value of 1 and would otherwise count as a valuable. OttoPay treats only
    /// the vanilla coin as currency.
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

        /// A trade that would credit nothing is refused rather than taking the valuables for free.
        internal bool Tradeable => Net > 0;
    }

    internal static TradeFees CurrentFees()
    {
        return new TradeFees(OttoAuraPlugin.AuraTradeFlatFee.Value, OttoAuraPlugin.AuraTradePercentFee.Value);
    }

    internal static bool IsValuable(ItemDrop.ItemData? item)
    {
        return IsValuable(item, OttoAuraPlugin.AuraTradeDeniedItems.Value);
    }

    internal static bool IsValuable(ItemDrop.ItemData? item, string deniedItems)
    {
        if (item == null || item.m_shared.m_value <= 0 || item.m_shared.m_name == CoinsName)
        {
            return false;
        }

        string prefab = item.m_dropPrefab != null ? item.m_dropPrefab.name : "";
        return !MoveEligibility.IsListed(deniedItems, prefab);
    }

    internal static long Worth(ItemDrop.ItemData item, int stack)
    {
        return (long)item.m_shared.m_value * stack;
    }

    internal static Quote QuoteFor(long gross)
    {
        return QuoteFor(gross, CurrentFees());
    }

    /// A gross that does not fit an int quotes as nothing, so it can never be traded. OttoPay's
    /// balance is an int too.
    internal static Quote QuoteFor(long gross, TradeFees fees)
    {
        if (gross <= 0 || gross > int.MaxValue)
        {
            return new Quote(0, 0);
        }

        // Percent is held as whole basis points and rounded up in integers: a float percent such
        // as 2.9 times 1000 lands a hair above 29 and would round up to 30.
        long basisPoints = (long)System.Math.Round(System.Math.Max(0f, fees.Percent) * 100.0);
        long percentPart = (gross * basisPoints + 9999) / 10000;
        long fee = System.Math.Max(0, fees.Flat) + percentPart;
        return new Quote((int)gross, (int)System.Math.Min(fee, gross));
    }
}
