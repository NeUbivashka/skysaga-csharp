namespace SkySaga.Game.Packets;

public class ExtraGuildIslandCreated : ISerializablePacket
{
    public Guid? IslandUuid;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.ExtraGuildIslandCreated);

        InventoryItemRef.WriteOptionalUuid(bitStream, IslandUuid);

        return bitStream;
    }
}
