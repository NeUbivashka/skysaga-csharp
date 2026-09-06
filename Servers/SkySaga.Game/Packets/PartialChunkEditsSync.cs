namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 14, server to client. Reader: FUN_007F8A60 -> FUN_00BB3E50(1, 0xE, ...).
///
///     ChunkCoord     FUN_007D6A30   X 6 bits (max 32), Y 3 bits (max 7), Z 6 bits (max 32)
///     ChunkEditList  FUN_007E79B0   count, then that many entries via FUN_007EA150
///     Unnamed bit    FUN_00EA6FA0   1 bit
///
/// Four things were wrong.
///
/// 1. Y was written 6 bits wide. The coordinate reader here is FUN_007D6A30 - the very same one
///    ChunkSync uses - and it reads Y as a ranged value with maximum 7, so 3 bits. Every packet
///    was three bits too long from the first field onwards.
///
/// 2. The list count was written with maximum 7. FUN_007D7140 reads it with maximum 8 and takes
///    the escape branch when the value comes back as exactly 8, so the field is 4 bits.
///
/// 3. The count was written as Count - 1. Nothing on the reading side adds it back, so every
///    list arrived one entry short.
///
/// 4. The trailing bit was not written at all.
///
/// NOT VERIFIED: the layout of an individual edit. The client reads them with FUN_007EA150 into
/// 0x14-byte slots, which is a useful cross-check if ChunkEdit.Serialize ever gets revisited, but
/// that class is untouched here and its field layout is still a guess.
/// </summary>
public class PartialChunkEditsSync : ISerializablePacket
{
    private const uint MaxAxisXZ = 32;
    private const uint MaxAxisY = 7;

    /// <summary>Above this the count carries an escape bit plus a full 32-bit count.</summary>
    private const uint ChunkEditCountMax = 8;

    public Vector3Int ChunkCoord;

    public List<ChunkEdit> ChunkEditList = [];

    /// <summary>Trailing flag. Purpose unknown - the reader takes one bit and names nothing.</summary>
    public bool Unnamed;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.PartialChunkEditsSync);

        bitStream.WriteRanged((uint)ChunkCoord.X, MaxAxisXZ);
        bitStream.WriteRanged((uint)ChunkCoord.Y, MaxAxisY);
        bitStream.WriteRanged((uint)ChunkCoord.Z, MaxAxisXZ);

        var count = (uint)ChunkEditList.Count;

        bitStream.WriteRanged(Math.Min(count, ChunkEditCountMax), ChunkEditCountMax);

        if (count >= ChunkEditCountMax)
        {
            bitStream.Write1();

            var countBytes = BitConverter.GetBytes(count);
            Array.Reverse(countBytes);
            bitStream.WriteBits(countBytes, 32, true);
        }

        foreach (var chunkEdit in ChunkEditList)
            chunkEdit.Serialize(bitStream);

        bitStream.Write(Unnamed);

        return bitStream;
    }
}
