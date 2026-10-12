namespace SkySaga.Game.Components;

public class ClientCharacterCustomisationComponent : Component
{
    public const uint CustomisationDataBits = 231;

    public byte[]? CustomisationData { get; set => SetIfChanged(ref field, value); }
    public bool IsCharacterCustomised { get; set => SetIfChanged(ref field, value); }

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(CustomisationData), StringComparison.OrdinalIgnoreCase))
        {
            if (CustomisationData is null)
                return false;

            bitStream.WriteBits(CustomisationData, CustomisationDataBits, true);

            return true;
        }
        else if (parameterName.Equals("CustomisationListCRC", StringComparison.OrdinalIgnoreCase)
                 || parameterName.Equals("ScaleRangeCRC", StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write0();

            return true;
        }
        else if (parameterName.Equals(nameof(IsCharacterCustomised), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write(IsCharacterCustomised);

            return true;
        }

        return false;
    }
}
