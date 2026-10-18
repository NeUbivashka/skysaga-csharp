namespace SkySaga.Game.Packets;

public class ExtraHomeIslandCreated : ISerializablePacket
{
    public Guid? IslandUuid;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.ExtraHomeIslandCreated);

        InventoryItemRef.WriteOptionalUuid(bitStream, IslandUuid);

        return bitStream;
    }
}
