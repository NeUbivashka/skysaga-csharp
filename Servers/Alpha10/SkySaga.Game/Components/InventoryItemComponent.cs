namespace SkySaga.Game.Components;

public class InventoryItemComponent : Component
{
    public InventorySlotData InventorySlotData { get; set => SetIfChanged(ref field, value); } = new();
    public bool ItemLocked { get; set => SetIfChanged(ref field, value); }
    public bool AllowAddingToFoundInBiomes { get; set => SetIfChanged(ref field, value); }
    public bool HasBeenTransferred { get; set => SetIfChanged(ref field, value); }

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(InventorySlotData), StringComparison.OrdinalIgnoreCase))
        {
            InventorySlotData.Serialize(bitStream);

            return true;
        }
        else if (parameterName.Equals(nameof(ItemLocked), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write(ItemLocked);

            return true;
        }
        else if (parameterName.Equals(nameof(AllowAddingToFoundInBiomes), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write(AllowAddingToFoundInBiomes);

            return true;
        }
        else if (parameterName.Equals(nameof(HasBeenTransferred), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write(HasBeenTransferred);

            return true;
        }

        return false;
    }
}