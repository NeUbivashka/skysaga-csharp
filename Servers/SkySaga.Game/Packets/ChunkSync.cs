namespace SkySaga.Game.Packets;

/// <summary>
/// Reader: FUN_007EE220 -> FUN_00BB3E50(1, 0xD, ...)
///   coords      FUN_007D6A30   X ranged max 32 (6 bits), Y ranged max 7 (3 bits), Z max 32
///   Data1       FUN_007E7DB0 -> FUN_007D6E60   presence bit, 32-bit length, then length bytes
///   Data2       same
///   Adjacent    FUN_007D6DF0   presence bit, then 7 bits ranged max 64, clamped
///
/// Coords and both arrays already matched. The one thing that did not: AdjacentChunks was going
/// out as a raw 8-bit write, and the client reads 7 bits (8 - NumBitsRequiredByte(0x40)) and
/// clamps the result to 64. It never bit us because the field has always been null, so only the
/// absent-bit went out - but the moment it carries a value the stream would slip by one bit and
/// every chunk after it would be garbage.
///
/// The 32-bit lengths stay big-endian. That is not a workaround: full-width fields are read
/// through FUN_006FB780, which byte-swaps while DAT_01449B5E is zero, and it is zero in this
/// build. These lengths are the evidence for that - the client accepts all 16 chunks - which is
/// what pinned the byte order for MapSpec.Seed and the rest.
/// </summary>
public class ChunkSync : ISerializablePacket
{
    private const uint MaxAxisXZ = 32;
    private const uint MaxAxisY = 7;

    /// <summary>Client reads 7 bits for this and clamps to 64.</summary>
    private const uint AdjacentChunksMax = 64;

    public Vector3Int Coords;

    public byte[]? Data1;
    public byte[]? Data2;

    public byte? AdjacentChunks;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.ChunkSync);

        bitStream.WriteRanged((uint)Coords.X, MaxAxisXZ);
        bitStream.WriteRanged((uint)Coords.Y, MaxAxisY);
        bitStream.WriteRanged((uint)Coords.Z, MaxAxisXZ);

        bitStream.WriteOptional(Data1, (value) =>
        {
            WriteLength(bitStream, value.Length);
            bitStream.WriteBits(value, (uint)value.Length * 8, true);
        });

        bitStream.WriteOptional(Data2, (value) =>
        {
            WriteLength(bitStream, value.Length);
            bitStream.WriteBits(value, (uint)value.Length * 8, true);
        });

        bitStream.WriteOptional(AdjacentChunks, (value) =>
        {
            bitStream.WriteRanged(Math.Min(value, AdjacentChunksMax), AdjacentChunksMax);
        });

        return bitStream;
    }

    private static void WriteLength(BitStream bitStream, int length)
    {
        var lengthBytes = BitConverter.GetBytes(length);

        Array.Reverse(lengthBytes);

        bitStream.WriteBits(lengthBytes, 32, true);
    }
}
