namespace SkySaga.Game.Components;

public class ClientCharacterStateComponent : Component
{
    public const uint CharacterStateMax = 10;
    public const uint MentalStateMax = 7;

    public static readonly string[] MentalStates = ["Normal", "Stun", "Dazed", "Sleep", "Resting", "Immobilised", "Vulnerable"];

    public byte CharacterStateId { get; set => SetIfChanged(ref field, value); }

    public byte CharacterMentalStateId { get; set => SetIfChanged(ref field, value); }

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(CharacterStateId), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteRanged(CharacterStateId, CharacterStateMax);
            return true;
        }

        if (parameterName.Equals(nameof(CharacterMentalStateId), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteRanged(CharacterMentalStateId, MentalStateMax);
            return true;
        }

        return false;
    }
}
