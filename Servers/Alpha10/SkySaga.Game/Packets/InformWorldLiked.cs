namespace SkySaga.Game.Packets;

public class InformWorldLiked : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.InformWorldLiked);

        return bitStream;
    }
}
