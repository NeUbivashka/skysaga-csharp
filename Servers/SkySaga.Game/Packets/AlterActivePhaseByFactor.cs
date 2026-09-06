namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 44, server to client. Reader: FUN_007EB3E0 -> FUN_00BB3E50(1, 0x2C, ...).
///
///     Factor    FUN_00CC1740   ranged, max 0x20 -> 6 bits, clamped to 0..32
///     EntityId  inline         32 bits, full width, so big-endian
///
/// The client scales the first field by DAT_012C1214, which reads 0x3D000000 - exactly 0.03125,
/// the same constant PlayerSpawned uses for its angles. So the wire value 0..32 becomes a factor
/// of 0.0 to 1.0 in steps of a thirty-second, which is what the name says it is.
///
/// Set Factor as the real number; the conversion and the clamp happen here.
/// </summary>
public class AlterActivePhaseByFactor : ISerializablePacket
{
    private const uint FactorMax = 0x20;

    /// <summary>Steps of 1/32 on the wire - anything finer is lost.</summary>
    private const float FactorScale = 0.03125f;

    /// <summary>0.0 to 1.0.</summary>
    public float Factor;

    public int EntityId;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.AlterActivePhaseByFactor);

        var units = (uint)Math.Clamp((int)(Factor / FactorScale), 0, (int)FactorMax);

        bitStream.WriteRanged(units, FactorMax);

        var idBytes = BitConverter.GetBytes(EntityId);
        Array.Reverse(idBytes);
        bitStream.WriteBits(idBytes, 32, true);

        return bitStream;
    }
}
