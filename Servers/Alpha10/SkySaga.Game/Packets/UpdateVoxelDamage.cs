namespace SkySaga.Game.Packets;

public class UpdateVoxelDamage : ISerializablePacket
{
    private const uint AmountMax = 0x7FFF;

    private const uint PositionHorizontalMax = 0x10000;
    private const uint PositionVerticalMax = 0x7F80;
    private const int PositionVerticalBias = 0x3FC0;
    private const float PositionScale = 64f;

    public int DamageId;
    public uint Amount;
    public int EntityId;
    public int SourceId;
    public Vector3 Position;
    public ulong Timestamp;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.UpdateVoxelDamage);

        InventoryItemRef.WriteUInt32(bitStream, (uint)DamageId);

        bitStream.WriteRanged(Math.Min(Amount, AmountMax), AmountMax);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);
        InventoryItemRef.WriteUInt32(bitStream, (uint)SourceId);

        WriteHorizontal(bitStream, Position.X);
        WriteVertical(bitStream, Position.Y);
        WriteHorizontal(bitStream, Position.Z);

        PvPWaitingInfo.WriteUInt64(bitStream, Timestamp);

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
}
