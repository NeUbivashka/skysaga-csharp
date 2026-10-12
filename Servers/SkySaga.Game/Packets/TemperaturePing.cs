namespace SkySaga.Game.Packets;

public class TemperaturePing : ISerializablePacket
{
    private const uint TemperatureMax = 0x40;

    public int EntityId;
    public uint Temperature;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.TemperaturePing);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);
        bitStream.WriteRanged(Math.Min(Temperature, TemperatureMax), TemperatureMax);

        return bitStream;
    }
}
