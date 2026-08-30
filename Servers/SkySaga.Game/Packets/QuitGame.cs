namespace SkySaga.Game.Packets;

public class QuitGame : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.QuitGame);

        return bitStream;
    }
}
