namespace SkySaga.Game.Packets;

public class DebugRequestAddItemToInventory : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugRequestAddItemToInventory);

        return bitStream;
    }
}
