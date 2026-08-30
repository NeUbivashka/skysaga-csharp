namespace SkySaga.Game.Packets;

public class PlayerLeft : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.PlayerLeft);

        return bitStream;
    }
}
