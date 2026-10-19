namespace SkySaga.Game.Packets;

public class StopCharge : ISerializablePacket
{
    public int EntityId;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.StopCharge);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);

        return bitStream;
    }
}
