namespace SkySaga.Game.Packets;

public class UnlockPosition : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.UnlockPosition);

        return bitStream;
    }
}
