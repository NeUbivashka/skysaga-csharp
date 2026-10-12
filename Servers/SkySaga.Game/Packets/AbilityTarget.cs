namespace SkySaga.Game.Packets;

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
