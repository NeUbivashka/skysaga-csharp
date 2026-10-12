namespace SkySaga.Game.Packets;

public class ProjectileImpacted : ISerializablePacket
{
    public int ProjectileId;
    public Vector3 Position;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.ProjectileImpacted);

        InventoryItemRef.WriteUInt32(bitStream, (uint)ProjectileId);
        WriteHorizontal(bitStream, Position.X);
        WriteVertical(bitStream, Position.Y);
        WriteHorizontal(bitStream, Position.Z);

        return bitStream;
    }

    private const uint PositionHorizontalMax = 0x10000;
    private const uint PositionVerticalMax = 0x7F80;
    private const int PositionVerticalBias = 0x3FC0;
    private const float PositionScale = 64f;

    private static void WriteHorizontal(BitStream bitStream, float value)
    {
        bitStream.WriteBits(BitConverter.GetBytes((int)(value * PositionScale)),
            32 - Util.NumBitsRequiredUInt32(PositionHorizontalMax), true);
    }

    private static void WriteVertical(BitStream bitStream, float value)
    {
        var units = Math.Clamp((int)(value * PositionScale) + PositionVerticalBias, 0, (int)PositionVerticalMax);
        bitStream.WriteBits(BitConverter.GetBytes(units),
            32 - Util.NumBitsRequiredUInt32(PositionVerticalMax), true);
    }
}
