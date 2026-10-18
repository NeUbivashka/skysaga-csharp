namespace SkySaga.Game.Packets;

public class DebugRemoveQuestFromVendor : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugRemoveQuestFromVendor);

        return bitStream;
    }
}
