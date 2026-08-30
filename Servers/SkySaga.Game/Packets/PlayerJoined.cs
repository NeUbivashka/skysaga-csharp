namespace SkySaga.Game.Packets;

public class PlayerJoined : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.PlayerJoined);

        return bitStream;
    }
}
