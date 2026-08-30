namespace SkySaga.Game.Packets;

public class PerformCraftingDropSlotAction : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.PerformCraftingDropSlotAction);

        return bitStream;
    }
}
