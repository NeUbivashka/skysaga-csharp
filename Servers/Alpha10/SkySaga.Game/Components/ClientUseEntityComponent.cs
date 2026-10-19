namespace SkySaga.Game.Components;

public class ClientUseEntityComponent : Component
{
    public int UsingEntityId { get; set => SetIfChanged(ref field, value); }

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(UsingEntityId), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteFullWidthUInt32((uint)UsingEntityId);

            return true;
        }

        return false;
    }
}
