namespace SkySaga.Game.Components;

public class ClientCharacterCustomisationComponent : Component
{
    public byte[]? CustomisationData { get; set => SetIfChanged(ref field, value is null ? null : CustomisationBlock.Upgrade(value)); }
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
            if (CustomisationData is null || CustomisationBlock.BitLength(CustomisationData) is not (> 0 and var bits))
                return false;

            bitStream.WriteBits(CustomisationData, (uint)bits, true);

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
