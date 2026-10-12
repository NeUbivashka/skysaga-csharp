namespace SkySaga.Game.Components;

public class ClientCharacterCustomisationComponent : Component
{
    public const uint CustomisationDataBits = 231;

    public byte[]? CustomisationData { get; set => SetIfChanged(ref field, value); }
    public bool IsCharacterCustomised { get; set => SetIfChanged(ref field, value); }

    public string? CustomisationListCrc
    {
        get;
        set
        {
            field = value;

            if (CustomisationData is null && value is not null && CustomisationTable.TryGet(value, out var data))
            {
                CustomisationData = data;
                IsCharacterCustomised = true;
            }
        }
    }

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(CustomisationData), StringComparison.OrdinalIgnoreCase))
        {
            if (CustomisationData is null)
                return false;

            bitStream.WriteBits(CustomisationData, CustomisationDataBits, true);

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
