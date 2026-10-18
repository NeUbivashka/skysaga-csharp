namespace SkySaga.Game.Packets;

public class InventorySort : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.InventorySort);

        return bitStream;
    }
}
