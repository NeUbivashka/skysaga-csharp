namespace SkySaga.Game.Packets;

public class MoveItemToCraftingDropSlot : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.MoveItemToCraftingDropSlot);

        return bitStream;
    }
}
