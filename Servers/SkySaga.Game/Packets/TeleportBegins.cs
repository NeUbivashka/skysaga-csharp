namespace SkySaga.Game.Packets;

public class TeleportBegins : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.TeleportBegins);

        return bitStream;
    }
}
