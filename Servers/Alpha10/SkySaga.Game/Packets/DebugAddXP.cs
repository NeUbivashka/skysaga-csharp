namespace SkySaga.Game.Packets;

public class DebugAddXP : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugAddXP);

        return bitStream;
    }
}
