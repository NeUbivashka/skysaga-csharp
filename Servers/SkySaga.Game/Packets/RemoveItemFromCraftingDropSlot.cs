namespace SkySaga.Game.Packets;

public class RemoveItemFromCraftingDropSlot : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.RemoveItemFromCraftingDropSlot);

        return bitStream;
    }
}
