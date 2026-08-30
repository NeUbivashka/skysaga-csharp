namespace SkySaga.Game.Packets;

public class TemperaturePing : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.TemperaturePing);

        return bitStream;
    }
}
