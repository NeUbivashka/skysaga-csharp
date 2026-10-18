namespace SkySaga.Game.Packets;

public class CoreTemperatureAdjustment : ISerializablePacket
{
    private const int TemperatureBias = 100;
    private const int TemperatureMin = -100;
    private const int TemperatureMax = 100;

    public int EntityId;

    public int Temperature;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.CoreTemperatureAdjustment);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);

        var raw = (byte)(Math.Clamp(Temperature, TemperatureMin, TemperatureMax) + TemperatureBias);

        bitStream.WriteBits([raw], 8, true);

        return bitStream;
    }
}
