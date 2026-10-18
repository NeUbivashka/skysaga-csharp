namespace SkySaga.Game.Packets;

public class SetConnectionTimeout : ISerializablePacket
{
    private const uint MaxMillis = 30000;

    public uint Millis;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.SetConnectionTimeout);

        var clamped = Math.Min(Millis, MaxMillis);

        bitStream.WriteRanged(clamped, MaxMillis);

        return bitStream;
    }
}
