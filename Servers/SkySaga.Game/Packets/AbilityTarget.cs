namespace SkySaga.Game.Packets;

/// <summary>
/// The nested value FUN_007E8220 reads, carried inside EntityPerformedAbility.
///
///     Flag        1 bit
///     Extents     FUN_007D6C10: three ranged components, max 0x400, 0xE0, 0x400 -> 11, 8, 11 bits
///     TargetId    32 bits, full width -> big-endian
///     Position    FUN_007D66A0: X 17 bits, Y 15 bits biased by -0x3FC0, Z 17 bits
///     Direction   FUN_007D6590: three components, ranged max 0x80 -> 8 bits each
///
/// The three FUN_0077A2B0 and FUN_0077A330 calls interleaved with these read nothing - they clamp
/// and zero locals. Extents is the one field whose bias is not established: the caller primes the
/// locals to zero rather than to a negative default the way Position and Direction are primed, so
/// it is written unbiased here.
/// </summary>
public class AbilityTarget
{
    private const uint ExtentXMax = 0x400;
    private const uint ExtentYMax = 0xE0;
    private const uint ExtentZMax = 0x400;

    private const uint PositionHorizontalMax = 0x10000;
    private const uint PositionVerticalMax = 0x7F80;
    private const int PositionVerticalBias = 0x3FC0;
    private const float PositionScale = 64f;

    private const uint DirectionMax = 0x80;
    private const int DirectionBias = 0x40;

    public bool Flag;

    public Vector3Int Extents;

    public int TargetId;

    public Vector3 Position;

    /// <summary>Each component is clamped to -64..+64 by the client.</summary>
    public Vector3Int Direction;

    public void Encode(BitStream bitStream)
    {
        bitStream.Write(Flag);

        bitStream.WriteRanged((uint)Math.Clamp(Extents.X, 0, (int)ExtentXMax), ExtentXMax);
        bitStream.WriteRanged((uint)Math.Clamp(Extents.Y, 0, (int)ExtentYMax), ExtentYMax);
        bitStream.WriteRanged((uint)Math.Clamp(Extents.Z, 0, (int)ExtentZMax), ExtentZMax);

        InventoryItemRef.WriteUInt32(bitStream, (uint)TargetId);

        WriteHorizontal(bitStream, Position.X);
        WriteVertical(bitStream, Position.Y);
        WriteHorizontal(bitStream, Position.Z);

        WriteDirection(bitStream, Direction.X);
        WriteDirection(bitStream, Direction.Y);
        WriteDirection(bitStream, Direction.Z);
    }

    internal static void WriteHorizontal(BitStream bitStream, float value)
    {
        bitStream.WriteBits(BitConverter.GetBytes((int)(value * PositionScale)),
            32 - Util.NumBitsRequiredUInt32(PositionHorizontalMax), true);
    }

    internal static void WriteVertical(BitStream bitStream, float value)
    {
        var units = Math.Clamp((int)(value * PositionScale) + PositionVerticalBias, 0, (int)PositionVerticalMax);
        bitStream.WriteBits(BitConverter.GetBytes(units),
            32 - Util.NumBitsRequiredUInt32(PositionVerticalMax), true);
    }

    internal static void WriteDirection(BitStream bitStream, int component)
    {
        bitStream.WriteRanged((uint)Math.Clamp(component + DirectionBias, 0, (int)DirectionMax), DirectionMax);
    }
}
