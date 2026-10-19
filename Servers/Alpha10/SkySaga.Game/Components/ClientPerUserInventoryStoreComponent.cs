namespace SkySaga.Game.Components;

public class ClientPerUserInventoryStoreComponent : Component
{
    public bool IsLootFountain { get; set => SetIfChanged(ref field, value); }

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(IsLootFountain), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write(IsLootFountain);

            return true;
        }

        return false;
    }
}
