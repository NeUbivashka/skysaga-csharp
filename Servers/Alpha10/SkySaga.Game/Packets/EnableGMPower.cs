namespace SkySaga.Game.Packets;

public class EnableGMPower : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.EnableGMPower);

        return bitStream;
    }
}
