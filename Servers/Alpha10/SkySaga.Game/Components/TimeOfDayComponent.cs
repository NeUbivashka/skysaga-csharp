namespace SkySaga.Game.Components;

public class TimeOfDayComponent : Component
{
    public int StartTimeOfDay { get; set => SetIfChanged(ref field, value); }
    public bool FixedTimeOfDay { get; set => SetIfChanged(ref field, value); }
    public int DayNightCycleDuration { get; set => SetIfChanged(ref field, value); }
    public ulong RealWorldStartTime { get; set => SetIfChanged(ref field, value); }
    public int TimeStretch { get; set => SetIfChanged(ref field, value); }
    public int TimeOfDayOffset { get; set => SetIfChanged(ref field, value); }

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