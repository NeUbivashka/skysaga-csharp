namespace SkySaga.Game.Components;

public class ClientDurabilityComponent : Component
{
    private const uint SmallValueMax = 0x7FFFF;

    public uint Durability { get; set => SetIfChanged(ref field, value); }

    public uint DurabilityMax { get; set => SetIfChanged(ref field, value); }

    public bool Indestructible { get; set => SetIfChanged(ref field, value); }

    public bool ShowOnUi { get; set => SetIfChanged(ref field, value); } = true;

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(Durability), StringComparison.OrdinalIgnoreCase))
        {
            WriteValue(bitStream, Durability);
            return true;
        }

        if (parameterName.Equals(nameof(DurabilityMax), StringComparison.OrdinalIgnoreCase))
        {
            WriteValue(bitStream, DurabilityMax);
            return true;
        }

        if (parameterName.Equals(nameof(Indestructible), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write(Indestructible);
            return true;
        }

        if (parameterName.Equals(nameof(ShowOnUi), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write(ShowOnUi);
            return true;
        }

        return false;
    }

    private static void WriteValue(BitStream bitStream, uint value)
    {
        var large = value > SmallValueMax;

        bitStream.Write(large);

        if (large)
            bitStream.WriteFullWidthUInt32(value);
        else
            bitStream.WriteRanged(value, SmallValueMax);
    }
}
