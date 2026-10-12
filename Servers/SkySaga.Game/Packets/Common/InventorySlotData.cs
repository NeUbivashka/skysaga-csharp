namespace SkySaga.Game.Packets.Common;

/// <summary>
/// inventorydefinition::ItemSlot, the value of inventoryitemcomponent.inventoryslotdata. Field
/// names from the client's reflection registration (FUN_00E02D10), wire format from its binary
/// reader FUN_00C08300:
///
///   ResourceNameCRC   ranged index into the item table (FUN_007D8110, 11 bits; see ItemTable).
///                     Despite the name it is not a CRC on the wire. Sending the CRC is what
///                     tripped the item-table index assert at 0x00BA8306.
///   Amount            1 bit "large", then 32 bits if set, else ranged max 0x40 (7 bits)
///                     (FUN_00C07960)
///   IsUnlimited       1 bit
///   LosableAmount     as Amount
///   CraftRandomSeed   ranged max 0x10000 (17 bits, FUN_00C078D0)
///   UUID              presence bit + 4 full-width u32 (FUN_007EA3B0)
/// </summary>
public class InventorySlotData : ISerializableType
{
    private const uint SmallAmountMax = 0x40;
    private const uint CraftRandomSeedMax = 0x10000;

    /// <summary>Lower-case CRC32 of the item name; converted to the table index when written.</summary>
    public uint? Name;

    public int Count;

    public bool IsUnlimited;

    public int LosableAmount;

    public int CraftRandomSeed;

    public string? ItemUUID;

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
