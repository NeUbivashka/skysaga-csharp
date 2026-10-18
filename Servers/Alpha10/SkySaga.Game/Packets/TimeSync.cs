namespace SkySaga.Game.Packets;

public class TimeSync : ISerializablePacket
{
    private const uint PhaseMax = 0xF;

    public uint Timestamp;

    public uint Phase;

    public uint Unnamed;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.TimeSync);

        InventoryItemRef.WriteUInt32(bitStream, Timestamp);

        bitStream.WriteRanged(Math.Min(Phase, PhaseMax), PhaseMax);

        InventoryItemRef.WriteUInt32(bitStream, Unnamed);

        return bitStream;
    }
}
