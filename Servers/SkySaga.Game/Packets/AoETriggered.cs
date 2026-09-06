namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 75. Reader: FUN_007EBBD0 -> FUN_00BB3E50(1, 0x4B, ...).
///
/// This one travels both ways - the client has a sender too (FUN_00A51520) - so the same layout
/// serves for receiving it later.
///
///     SourceId    FUN_007EA4B0: 1 presence bit + 32 bits
///     Shape       FUN_007D6410: ranged, max 0x38 -> 6 bits
///     Position    FUN_007D66A0: X 17 bits, Y 15 bits biased by -0x3FC0, Z 17 bits
///     Direction   FUN_007D6590: three components, ranged max 0x80 -> 8 bits each
///     Radius      FUN_007D6390: ranged, max 0x18 -> 5 bits
///
/// Same position reader as PlayerSpawned and EventEffect, so the same 64-units-per-world-unit
/// scale and the same narrow biased Y. Direction is clamped to +-0x40 by FUN_0077A330 right after
/// it is read, which is where the bias here comes from.
/// </summary>
public class AoETriggered : ISerializablePacket
{
    private const uint ShapeMax = 0x38;
    private const uint RadiusMax = 0x18;

    private const uint PositionHorizontalMax = 0x10000;
    private const uint PositionVerticalMax = 0x7F80;
    private const int PositionVerticalBias = 0x3FC0;
    private const float PositionScale = 64f;

    private const uint DirectionMax = 0x80;
    private const int DirectionBias = 0x40;

    public uint? SourceId;

    public uint Shape;

    public Vector3 Position;

    /// <summary>Each component is clamped to -64..+64 by the client.</summary>
    public Vector3Int Direction;

    public uint Radius;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.AoETriggered);

        InventoryItemRef.WriteOptionalUInt32(bitStream, SourceId);

        bitStream.WriteRanged(Math.Min(Shape, ShapeMax), ShapeMax);

        WriteHorizontal(bitStream, Position.X);
        WriteVertical(bitStream, Position.Y);
        WriteHorizontal(bitStream, Position.Z);

        WriteDirection(bitStream, Direction.X);
        WriteDirection(bitStream, Direction.Y);
        WriteDirection(bitStream, Direction.Z);

        bitStream.WriteRanged(Math.Min(Radius, RadiusMax), RadiusMax);

        return bitStream;
    }

    private static void WriteHorizontal(BitStream bitStream, float value)
    {
        bitStream.WriteBits(
            BitConverter.GetBytes((int)(value * PositionScale)),
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
        bitStream.WriteRanged((uint)Math.Clamp(component + DirectionBias, 0, (int)DirectionMax), DirectionMax);
    }
}
