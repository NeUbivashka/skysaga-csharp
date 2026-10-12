namespace SkySaga.Game.Packets;

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
