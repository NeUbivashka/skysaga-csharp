namespace SkySaga.Game.Packets;

public class TemperaturePing : ISerializablePacket
{
    private const int TemperatureBias = 32;
    private const int TemperatureMin = -32;
    private const int TemperatureMax = 32;

    public int EntityId;

    public int Temperature;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.TemperaturePing);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);
        bitStream.WriteRanged((uint)(Math.Clamp(Temperature, TemperatureMin, TemperatureMax) + TemperatureBias), TemperatureMax + TemperatureBias);

        return bitStream;
    }
}
