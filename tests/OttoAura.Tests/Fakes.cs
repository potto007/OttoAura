using OttoAura.AuraTrade;

namespace OttoAura.Tests;

/// A trade desk that never touches OttoPay or the world and keeps a ledger of every call.
internal sealed class FakeDesk : ITradeDesk
{
    internal readonly List<int> Deposits = new();
    internal readonly List<ItemDrop.ItemData> Dropped = new();

    internal bool AcceptsDeposits { get; set; } = true;

    public bool TryDeposit(int coins)
    {
        Deposits.Add(coins);
        return AcceptsDeposits;
    }

    public void Drop(ItemDrop.ItemData item)
    {
        Dropped.Add(item);
    }
}

internal static class Items
{
    internal static ItemDrop.ItemData Valuable(string name, int value, int stack, int maxStack = 50)
    {
        return new ItemDrop.ItemData
        {
            m_shared = new ItemDrop.ItemData.SharedData { m_name = name, m_value = value, m_maxStackSize = maxStack },
            m_stack = stack,
        };
    }

    internal static ItemDrop.ItemData Gear(string name, float durability, float maxDurability = 100f, bool canBeRepaired = true, bool useDurability = true)
    {
        return new ItemDrop.ItemData
        {
            m_shared = new ItemDrop.ItemData.SharedData
            {
                m_name = name,
                m_maxStackSize = 1,
                m_maxDurability = maxDurability,
                m_useDurability = useDurability,
                m_canBeReparied = canBeRepaired,
            },
            m_durability = durability,
            m_quality = 1,
        };
    }

    internal static Inventory Pack(int width, int height)
    {
        return new Inventory("pack", null, width, height);
    }
}
