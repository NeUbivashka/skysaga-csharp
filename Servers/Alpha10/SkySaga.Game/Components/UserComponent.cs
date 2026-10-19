namespace SkySaga.Game.Components;

public class UserComponent : Component
{
    public Guid? CharacterUuid { get; set => SetIfChanged(ref field, value); }

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(CharacterUuid), StringComparison.OrdinalIgnoreCase))
        {
            if (CharacterUuid is null)
                return false;

            bitStream.WriteClientUuid(CharacterUuid);

            return true;
        }

        return false;
    }
}
