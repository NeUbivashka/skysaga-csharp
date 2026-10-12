namespace SkySaga.Game.Components;

public class InventoryComponent : Component
{
    private const uint MaxInventorySlotsMax = 0xF0;
    private const uint EntityListEscape = 0xF9;

    public byte MaxInventorySlots { get; set => SetIfChanged(ref field, value); }
    public uint? InventoryLoadOut { get; set => SetIfChanged(ref field, value); }
    public bool TakeOnly { get; set => SetIfChanged(ref field, value); }

    public List<int> InventoryEntityList { get; set => SetIfChanged(ref field, value); } = [];

    public int VaultInventoryEntity { get; set => SetIfChanged(ref field, value); }

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
        else if (parameterName.Equals(nameof(VaultInventoryEntity), StringComparison.OrdinalIgnoreCase))
        {
            if (VaultInventoryEntity == 0)
                return false;

            bitStream.WriteFullWidthUInt32((uint)VaultInventoryEntity);

            return true;
        }

        return false;
    }
}
