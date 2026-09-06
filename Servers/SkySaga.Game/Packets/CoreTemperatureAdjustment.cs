namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 89, server to client. Reader: FUN_007EED00 -> FUN_00BB3E50(1, 0x59, ...).
///
///     EntityId      32 bits, full width -> big-endian
///     Temperature   FUN_007D6D00: 8 bits, then the client subtracts 100 and clamps to -100..100
///
/// The width helper here is FUN_00EA7360, which had not turned up before: it is the 8-bit sibling
/// of FUN_00EA7260, returning 8 minus the bits needed for its argument. Called with 0xC8 (200) it
/// returns 0, so the field is a full 8 bits carrying 0..200, and the client reads it as a signed
/// temperature by taking away the 100 bias.
///
/// Set Temperature as the real signed value; the bias and the clamp happen here.
/// </summary>
public class CoreTemperatureAdjustment : ISerializablePacket
{
    private const int TemperatureBias = 100;
    private const int TemperatureMin = -100;
    private const int TemperatureMax = 100;

    public int EntityId;

    /// <summary>-100 to +100.</summary>
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
