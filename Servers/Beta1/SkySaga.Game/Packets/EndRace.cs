namespace SkySaga.Game.Packets;

public class EndRace : ISerializablePacket
{
    public Guid? RaceUuid;
    public int Result;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.EndRace);

        InventoryItemRef.WriteOptionalUuid(bitStream, RaceUuid);
        InventoryItemRef.WriteUInt32(bitStream, (uint)Result);

        return bitStream;
    }
}
