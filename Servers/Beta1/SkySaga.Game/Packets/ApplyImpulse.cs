namespace SkySaga.Game.Packets;

public class ApplyImpulse : ISerializablePacket
{
    private const uint DirectionMax = 0x80;
    private const int DirectionBias = 0x40;
    private const uint StrengthMax = 0xFC;

    public int EntityId;

    public Vector3Int Direction;

    public uint Strength;
    public bool Flag;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.ApplyImpulse);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);

        WriteDirection(bitStream, Direction.X);
        WriteDirection(bitStream, Direction.Y);
        WriteDirection(bitStream, Direction.Z);

        bitStream.WriteRanged(Math.Min(Strength, StrengthMax), StrengthMax);
        bitStream.Write(Flag);

        return bitStream;
    }

    private static void WriteDirection(BitStream bitStream, int component)
    {
        bitStream.WriteRanged((uint)Math.Clamp(component + DirectionBias, 0, (int)DirectionMax), DirectionMax);
    }
}
