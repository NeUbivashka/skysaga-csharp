namespace SkySaga.Game.Packets;

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
