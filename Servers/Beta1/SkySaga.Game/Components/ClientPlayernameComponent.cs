namespace SkySaga.Game.Components;

public class ClientPlayernameComponent : Component
{
    public const uint SigilTableCount = 27;
    private const uint SigilListEscape = 0x100;

    public uint CurrentSigil { get; set => SetIfChanged(ref field, value); }

    public List<(uint Index, uint A, uint B)> AvailablePlayerSigilList { get; set => SetIfChanged(ref field, value); } = [];

    public string? Playername
    {
        get;
        set => SetIfChanged(ref field, value);
    }

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(Playername), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteString(Playername ?? " - ");

            return true;
        }

        if (parameterName.Equals(nameof(CurrentSigil), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteRangedIndex(CurrentSigil, SigilTableCount);

            return true;
        }

        if (parameterName.Equals(nameof(AvailablePlayerSigilList), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteEscapedCount((uint)AvailablePlayerSigilList.Count, SigilListEscape);

            foreach (var (index, a, b) in AvailablePlayerSigilList)
            {
                bitStream.WriteRangedIndex(index, SigilTableCount);
                bitStream.WriteFullWidthUInt32(a);
                bitStream.WriteFullWidthUInt32(b);
            }

            return true;
        }

        return false;
    }
}
