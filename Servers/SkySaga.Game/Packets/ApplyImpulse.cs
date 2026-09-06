namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 87, server to client. Reader: FUN_007EC0C0 -> FUN_00BB3E50(1, 0x57, ...).
///
///     EntityId    32 bits, full width -> big-endian
///     Direction   FUN_007D6590: three components, ranged max 0x80 -> 8 bits each
///     Strength    FUN_007D6310: ranged, max 0xFC -> 8 bits
///     Flag        1 bit
///
/// Direction is the same reader AoETriggered and EventEffect use, and the same FUN_0077A330 clamp
/// to +-0x40 follows it - hence the bias here.
/// </summary>
public class ApplyImpulse : ISerializablePacket
{
    private const uint DirectionMax = 0x80;
    private const int DirectionBias = 0x40;
    private const uint StrengthMax = 0xFC;

    public int EntityId;

    /// <summary>Each component is clamped to -64..+64 by the client.</summary>
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
