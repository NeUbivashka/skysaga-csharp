namespace SkySaga.Game.Components;

public class ClientOwnerComponent : Component
{
    public Guid? Owner { get; set => SetIfChanged(ref field, value); }

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(Owner), StringComparison.OrdinalIgnoreCase))
        {
            if (Owner is null)
                return false;

            bitStream.WriteClientUuid(Owner);

            return true;
        }

        return false;
    }
}
