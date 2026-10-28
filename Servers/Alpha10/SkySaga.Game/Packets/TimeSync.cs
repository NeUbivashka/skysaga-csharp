namespace SkySaga.Game.Packets;

public class TimeSync : ISerializablePacket
{
    private const uint TimeScaleMax = 0xF;

    public ulong GameTimeMs;

    public uint TimeScale = 1;

    public ulong RealTimeMs;

    public static TimeSync Now()
    {
        var now = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        return new TimeSync { GameTimeMs = now, RealTimeMs = now };
    }

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.TimeSync);
        bitStream.WriteUInt64(GameTimeMs);
        bitStream.WriteRanged(Math.Min(TimeScale, TimeScaleMax), TimeScaleMax);
        bitStream.WriteUInt64(RealTimeMs);

        return bitStream;
    }
}
