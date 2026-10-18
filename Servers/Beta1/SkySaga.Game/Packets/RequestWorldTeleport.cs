namespace SkySaga.Game.Packets;

public class RequestWorldTeleport : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.RequestWorldTeleport);

        return bitStream;
    }
}
