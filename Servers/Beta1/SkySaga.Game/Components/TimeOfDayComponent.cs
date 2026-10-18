namespace SkySaga.Game.Components;

public class TimeOfDayComponent : Component
{
    public int StartTimeOfDay { get; set => SetIfChanged(ref field, value); }
    public bool FixedTimeOfDay { get; set => SetIfChanged(ref field, value); }
    public int DayNightCycleDuration { get; set => SetIfChanged(ref field, value); }
    public ulong RealWorldStartTime { get; set => SetIfChanged(ref field, value); }
    public int TimeStretch { get; set => SetIfChanged(ref field, value); }
    public int TimeOfDayOffset { get; set => SetIfChanged(ref field, value); }

    public const int DayLength = 0x10000;
    public const int NormalStretch = 64;

    public bool IsFrozen => TimeStretch == 0;

    public int CurrentTime(ulong now)
    {
        if (IsFrozen || DayNightCycleDuration <= 0)
            return TimeOfDayOffset;

        var cycleMs = DayNightCycleDuration / 64.0 * 60000.0 * NormalStretch / TimeStretch;
        var elapsed = now > RealWorldStartTime ? now - RealWorldStartTime : 0;

        return (int)((TimeOfDayOffset + elapsed / cycleMs * DayLength) % DayLength);
    }

    public void SetTime(int time, ulong now)
    {
        time = ((time % DayLength) + DayLength) % DayLength;

        StartTimeOfDay = time;
        TimeOfDayOffset = time;
        RealWorldStartTime = now;
    }

    public void SetFrozen(bool frozen, ulong now)
    {
        if (frozen == IsFrozen)
            return;

        SetTime(CurrentTime(now), now);
        TimeStretch = frozen ? 0 : NormalStretch;
    }

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(StartTimeOfDay), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteBits(BitConverter.GetBytes(StartTimeOfDay), 32 - Util.NumBitsRequiredUInt32(0x10000), true);

            return true;
        }
        else if (parameterName.Equals(nameof(FixedTimeOfDay), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write(FixedTimeOfDay);

            return true;
        }
        else if (parameterName.Equals(nameof(DayNightCycleDuration), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteBits(BitConverter.GetBytes(DayNightCycleDuration), 32 - Util.NumBitsRequiredUInt32(1920), true);

            return true;
        }
        else if (parameterName.Equals(nameof(RealWorldStartTime), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteUInt64(RealWorldStartTime);

            return true;
        }
        else if (parameterName.Equals(nameof(TimeStretch), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteBits(BitConverter.GetBytes(TimeStretch), 32 - Util.NumBitsRequiredUInt32(8128), true);

            return true;
        }
        else if (parameterName.Equals(nameof(TimeOfDayOffset), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteBits(BitConverter.GetBytes(TimeOfDayOffset), 32 - Util.NumBitsRequiredUInt32(0x10000), true);

            return true;
        }

        return false;
    }
}