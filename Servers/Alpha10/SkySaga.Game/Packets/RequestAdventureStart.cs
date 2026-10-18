namespace SkySaga.Game.Packets;

public class RequestAdventureStart : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.RequestAdventureStart);

        return bitStream;
    }
}
