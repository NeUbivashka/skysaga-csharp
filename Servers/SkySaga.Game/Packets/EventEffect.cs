namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 50, server to client. Reader: FUN_007F3280 -> FUN_00BB3E50(1, 0x32, ...).
///
///     EffectIndex   ranged, max 0xD0 -> 8 bits
///     EntityId      32 bits, full width -> big-endian
///     SourceId      32 bits, full width -> big-endian
///     ParamA        FUN_007D8110   16 bits
///     ParamB        FUN_007D8110   16 bits
///     Position      FUN_007D66A0   X 17 bits, Y 15 bits biased by -0x3FC0, Z 17 bits
///     Direction     FUN_007D6590   three components, ranged max 0x80 -> 8 bits each
///     Magnitude     FUN_007D76B0   ranged, max 0x7FFF -> 15 bits
///
/// FUN_007D66A0 is the same position reader PlayerSpawned uses, so the scale is 64 units per world
/// unit and Y is the narrow, biased one. Do not copy the entity-sync encoding here.
///
/// Between the position and the direction the stub calls FUN_0077A330, which reads nothing at all -
/// it copies three ints and clamps each to +-0x40. That clamp is why Direction is treated as a
/// biased value here: the wire carries 0..128 and the client works in -64..+64.
/// </summary>
public class EventEffect : ISerializablePacket
{
    private const uint EffectIndexMax = 0xD0;
    private const uint MagnitudeMax = 0x7FFF;

    private const uint PositionHorizontalMax = 0x10000;
    private const uint PositionVerticalMax = 0x7F80;
    private const int PositionVerticalBias = 0x3FC0;
    private const float PositionScale = 64f;

    private const uint DirectionMax = 0x80;
    private const int DirectionBias = 0x40;

    public uint EffectIndex;

    public int EntityId;

    public int SourceId;

    public ushort ParamA;

    public ushort ParamB;

    public Vector3 Position;

    /// <summary>Each component is clamped to -64..+64 by the client.</summary>
    public Vector3Int Direction;

    public uint Magnitude;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.EventEffect);

        bitStream.WriteRanged(Math.Min(EffectIndex, EffectIndexMax), EffectIndexMax);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);
        InventoryItemRef.WriteUInt32(bitStream, (uint)SourceId);

        bitStream.WriteBits(BitConverter.GetBytes(ParamA), 16, true);
        bitStream.WriteBits(BitConverter.GetBytes(ParamB), 16, true);

        WriteHorizontal(bitStream, Position.X);
        WriteVertical(bitStream, Position.Y);
        WriteHorizontal(bitStream, Position.Z);

        WriteDirection(bitStream, Direction.X);
        WriteDirection(bitStream, Direction.Y);
        WriteDirection(bitStream, Direction.Z);

        bitStream.WriteRanged(Math.Min(Magnitude, MagnitudeMax), MagnitudeMax);

        return bitStream;
    }

    private static void WriteHorizontal(BitStream bitStream, float value)
    {
        var units = (int)(value * PositionScale);

        bitStream.WriteBits(
            BitConverter.GetBytes(units),
            32 - Util.NumBitsRequiredUInt32(PositionHorizontalMax),
            true);
    }

    private static void WriteVertical(BitStream bitStream, float value)
    {
        var units = Math.Clamp((int)(value * PositionScale) + PositionVerticalBias, 0, (int)PositionVerticalMax);

        bitStream.WriteBits(
            BitConverter.GetBytes(units),
            32 - Util.NumBitsRequiredUInt32(PositionVerticalMax),
            true);
    }

    private static void WriteDirection(BitStream bitStream, int component)
    {
        var units = (uint)Math.Clamp(component + DirectionBias, 0, (int)DirectionMax);

        bitStream.WriteRanged(units, DirectionMax);
    }
}
