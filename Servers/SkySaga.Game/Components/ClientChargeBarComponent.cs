namespace SkySaga.Game.Components;

public class ClientChargeBarComponent : Component
{
    public const uint ChargeMax = 40000;
    public const uint LockIndexMax = 3;

    public uint CurrentCharge { get; set => SetIfChanged(ref field, Math.Min(value, ChargeMax)); } = ChargeMax;

    public uint? SecondaryCharge { get; set => SetIfChanged(ref field, value is { } v ? Math.Min(v, ChargeMax) : null, nameof(CurrentCharge)); }

    public uint CurrentLockIndex { get; set => SetIfChanged(ref field, Math.Min(value, LockIndexMax)); }

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(CurrentCharge), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteRanged(CurrentCharge, ChargeMax);
            bitStream.Write(SecondaryCharge.HasValue);

            if (SecondaryCharge is { } secondary)
                bitStream.WriteRanged(secondary, ChargeMax);

            return true;
        }

        if (parameterName.Equals(nameof(CurrentLockIndex), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteRanged(CurrentLockIndex, LockIndexMax);

            return true;
        }

        return false;
    }
}
