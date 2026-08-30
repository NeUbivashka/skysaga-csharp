namespace SkySaga.Game.Packets;

public class PlayerDodged : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.PlayerDodged);

        return bitStream;
    }
}
