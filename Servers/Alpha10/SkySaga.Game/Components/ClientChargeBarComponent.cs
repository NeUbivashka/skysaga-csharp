namespace SkySaga.Game.Components;

public class ClientChargeBarComponent : Component
{
    public const uint ChargeMax = 40000;
    public const uint LockIndexMax = 3;
    public const float UnitsPerPoint = 8;
    private const double DecaySyncSeconds = 1;

    private DateTime _lastGainUtc;
    private DateTime _lastDecayUtc;
    private float _points;

    public uint CurrentCharge { get; set => SetIfChanged(ref field, Math.Min(value, ChargeMax)); }

    public uint? SecondaryCharge { get; set => SetIfChanged(ref field, value is { } v ? Math.Min(v, ChargeMax) : null, nameof(CurrentCharge)); }

    public uint CurrentLockIndex { get; set => SetIfChanged(ref field, Math.Min(value, LockIndexMax)); }

    public void Gain(float points, DateTime now)
    {
        if (points <= 0)
            return;

        _points = Math.Min(_points + points, ChargeBarTable.Max);
        _lastGainUtc = now;
        _lastDecayUtc = now;
        CurrentCharge = (uint)MathF.Round(_points * UnitsPerPoint);
    }

    public void Decay(DateTime now)
    {
        if (_points <= 0 || (now - _lastGainUtc).TotalSeconds < ChargeBarTable.PauseSeconds || (now - _lastDecayUtc).TotalSeconds < DecaySyncSeconds)
            return;

        var elapsed = Math.Min((now - _lastDecayUtc).TotalSeconds, (now - _lastGainUtc).TotalSeconds - ChargeBarTable.PauseSeconds);
        _lastDecayUtc = now;
        _points = Math.Max(_points + ChargeBarTable.RecoveryRate * (float)elapsed, 0);
        CurrentCharge = (uint)MathF.Round(_points * UnitsPerPoint);
    }

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
