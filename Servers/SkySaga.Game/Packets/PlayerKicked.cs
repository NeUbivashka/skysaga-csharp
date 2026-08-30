namespace SkySaga.Game.Packets;

public class PlayerKicked : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.PlayerKicked);

        return bitStream;
    }
}
