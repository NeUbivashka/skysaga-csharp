namespace SkySaga.Game.Packets;

public class PlayerUnfriended : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.PlayerUnfriended);

        return bitStream;
    }
}
