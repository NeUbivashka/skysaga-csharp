namespace SkySaga.Game.Packets;

public class PromotionAddedToIsland : ISerializablePacket
{
    public int PromotionId;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.PromotionAddedToIsland);

        InventoryItemRef.WriteUInt32(bitStream, (uint)PromotionId);

        return bitStream;
    }
}
