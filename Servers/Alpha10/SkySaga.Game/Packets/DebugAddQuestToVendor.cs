namespace SkySaga.Game.Packets;

public class DebugAddQuestToVendor : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugAddQuestToVendor);

        return bitStream;
    }
}
