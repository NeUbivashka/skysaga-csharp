namespace SkySaga.Game.Packets;

public class DebugAdjustEntityHP : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugAdjustEntityHP);

        return bitStream;
    }
}
