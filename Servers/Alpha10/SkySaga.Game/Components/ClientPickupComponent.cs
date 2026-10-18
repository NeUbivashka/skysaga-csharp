namespace SkySaga.Game.Components;

public class ClientPickupComponent : Component
{
    public InventoryItemRef? InventoryItemSpec { get; set => SetIfChanged(ref field, value); }

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (!parameterName.Equals(nameof(InventoryItemSpec), StringComparison.OrdinalIgnoreCase) || InventoryItemSpec is null)
            return false;

        InventoryItemSpec.Encode(bitStream);

        return true;
    }
}
