namespace SkySaga.Game.Components;

/// <summary>
/// Inventory. Client bindings read function FUN_00C40CC0 (vtable 0x01345FBC); binding indices:
/// MaxInventorySlots 0, InventoryLoadOut 1, TakeOnly 2, InventoryEntityList 3, CurrencySourceType 4,
/// NonStoredItemsPendingDeletion 5, VaultInventoryEntity 6, InventoryComponentData 7.
///
///   maxinventoryslots     ranged byte, max 0xF0 = 8 bits (FUN_00C3F490)
///   inventoryloadout      presence bit + full-width u32 (FUN_007EA4B0)
///   takeonly              1 bit
///   inventoryentitylist   count escaped at 0xF9 (8 bits, FUN_00BC6BB0), then full-width u32
///                         entity ids (FUN_00BC7550)
///
/// The old writer sent maxinventoryslots in 6 bits and the list with a 6-bit count and
/// little-endian ids. Every Player parameter after sync index 81 was read shifted.
/// </summary>
public class InventoryComponent : Component
{
    private const uint MaxInventorySlotsMax = 0xF0;
    private const uint EntityListEscape = 0xF9;

    public byte MaxInventorySlots { get; set => SetIfChanged(ref field, value); }
    public uint? InventoryLoadOut { get; set => SetIfChanged(ref field, value); }
    public bool TakeOnly { get; set => SetIfChanged(ref field, value); }

    public List<int> InventoryEntityList { get; set => SetIfChanged(ref field, value); } = [];

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(MaxInventorySlots), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteRanged(Math.Min(MaxInventorySlots, MaxInventorySlotsMax), MaxInventorySlotsMax);

            return true;
        }
        else if (parameterName.Equals(nameof(InventoryLoadOut), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write(InventoryLoadOut.HasValue);

            if (InventoryLoadOut is { } loadOut)
                bitStream.WriteFullWidthUInt32(loadOut);

            return true;
        }
        else if (parameterName.Equals(nameof(TakeOnly), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write(TakeOnly);

            return true;
        }
        else if (parameterName.Equals(nameof(InventoryEntityList), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteEscapedCount((uint)InventoryEntityList.Count, EntityListEscape);

            foreach (var inventoryEntity in InventoryEntityList)
                bitStream.WriteFullWidthUInt32((uint)inventoryEntity);

            return true;
        }

        return false;
    }
}
