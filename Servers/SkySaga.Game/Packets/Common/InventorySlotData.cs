namespace SkySaga.Game.Packets.Common;

public class InventorySlotData : ISerializableType
{
    private const uint SmallAmountMax = 0x40;
    private const uint CraftRandomSeedMax = 0x10000;

    public uint? Name;

    public int Count;

    public bool IsUnlimited;

    public int LosableAmount;

    public int CraftRandomSeed;

    public string? ItemUUID;

    public InventorySlotData WithCount(int count)
    {
        var copy = (InventorySlotData)MemberwiseClone();
        copy.Count = count;
        return copy;
    }

    public void Serialize(BitStream bitStream)
    {
        bitStream.WriteRangedIndex(Name is { } crc ? ItemTable.IndexOf(crc) : 0u, ItemTable.Count);

        WriteAmount(bitStream, Count);

        bitStream.Write(IsUnlimited);

        WriteAmount(bitStream, LosableAmount);

        bitStream.WriteRanged((uint)Math.Clamp(CraftRandomSeed, 0, (int)CraftRandomSeedMax), CraftRandomSeedMax);

        bitStream.WriteClientUuid(Guid.TryParse(ItemUUID, out var uuid) ? uuid : null);
    }

    private static void WriteAmount(BitStream bitStream, int amount)
    {
        var value = (uint)Math.Max(amount, 0);
        var large = value > SmallAmountMax;

        bitStream.Write(large);

        if (large)
            bitStream.WriteBits(BitConverter.GetBytes(value), 32, true);
        else
            bitStream.WriteRanged(value, SmallAmountMax);
    }
}
