namespace SkySaga.Game.Packets;

public class CollectCraftedItemInSlot : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.CollectCraftedItemInSlot);

        return bitStream;
    }
}
