namespace SkySaga.Game.Components;

public class ClientPickupComponent : Component
{
    public InventoryItemRef? InventoryItemSpec { get; set => SetIfChanged(ref field, value); }
    public bool? CanBePickedUp { get; set => SetIfChanged(ref field, value); }

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(CanBePickedUp), StringComparison.OrdinalIgnoreCase))
        {
            if (CanBePickedUp is not bool canBePickedUp)
                return false;

            bitStream.Write(canBePickedUp);
            return true;
        }

        if (!parameterName.Equals(nameof(InventoryItemSpec), StringComparison.OrdinalIgnoreCase) || InventoryItemSpec is null)
            return false;

        InventoryItemSpec.Encode(bitStream);

        return true;
    }
}
