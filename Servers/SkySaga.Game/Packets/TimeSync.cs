namespace SkySaga.Game.Packets;

public class TimeSync : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.TimeSync);

        return bitStream;
    }
}
