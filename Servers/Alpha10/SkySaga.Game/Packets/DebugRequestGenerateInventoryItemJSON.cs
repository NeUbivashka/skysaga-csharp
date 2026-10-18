namespace SkySaga.Game.Packets;

public class DebugRequestGenerateInventoryItemJSON : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugRequestGenerateInventoryItemJSON);

        return bitStream;
    }
}
