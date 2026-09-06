namespace SkySaga.Game.Packets;

/// <summary>
/// Field order and widths match the client (reader FUN_007E9E80, names from FUN_00805F80):
/// MapSpec, AdventureType, MapFileName, GroupLock, GameQueueCRC, GameMode, and one unnamed
/// 32-bit field.
///
/// The only thing that was wrong here was byte order. GameQueueCRC and Unnamed are full-width
/// 32-bit fields, and the client byte-swaps those (FUN_006FB780 path), so they have to go out
/// big-endian - the same way ChunkSync already writes its data lengths. Ranged fields such as
/// GameMode are read through the opposite predicate and stay little-endian.
/// </summary>
public class MapDefinition : ISerializablePacket
{
    public Vector3Int MapSizeChunks;

    public MapSpec Spec = new();

    public string AdventureType = string.Empty;

    public string MapFileName = string.Empty;

    public Guid? GroupLock;

    public uint? GameQueueCRC;

    public uint GameMode;

    private const uint GameModeMax = 0x10;

    public uint Unnamed;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.MapDefinition);

        Spec.Encode(bitStream);

        bitStream.WriteString(AdventureType);
        bitStream.WriteString(MapFileName);

        bitStream.WriteGuid(GroupLock);

        bitStream.WriteOptional(GameQueueCRC, (value) =>
        {
            WriteUInt32(bitStream, value);
        });

        bitStream.WriteRanged(GameMode, GameModeMax);

        WriteUInt32(bitStream, Unnamed);

        return bitStream;
    }

    /// <summary>Full-width 32-bit fields go out big-endian - see the note on the class.</summary>
    private static void WriteUInt32(BitStream bitStream, uint value)
    {
        var bytes = BitConverter.GetBytes(value);

        Array.Reverse(bytes);

        bitStream.WriteBits(bytes, 32, true);
    }
}
