namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 25, server to client. Reader: FUN_007FE9F0 -> FUN_00BB3E50(1, 0x19, ...).
///
///     Entries   FUN_007E7220   count via FUN_007D7020: ranged max 4 -> 3 bits, escape at 4
///                              (1 bit + 32 bits); then count x FUN_007E9190
///     Unnamed   32 bits, full width -> big-endian
///
/// Each entry, from FUN_007E9190:
///
///     Name      string
///     ValueA    1 presence bit + 32 bits
///     ValueB    1 presence bit + 32 bits
///     Rank      ranged, max 3 -> 2 bits
///     ScoreA    32 bits, full width -> big-endian
///     ScoreB    32 bits, full width -> big-endian
///
/// Names by role - a name, two optional numbers, a small rank and two scores is what an end-of-game
/// scoreboard row looks like, but the loggers give up nothing here.
/// </summary>
public class SetEndGameList : ISerializablePacket
{
    /// <summary>Above this the count carries an escape bit plus a full 32-bit count.</summary>
    private const uint EntryCountMax = 4;

    private const uint RankMax = 3;

    public List<Entry> Entries = [];

    public uint Unnamed;

    public class Entry
    {
        public string? Name;
        public uint? ValueA;
        public uint? ValueB;
        public uint Rank;
        public int ScoreA;
        public int ScoreB;
    }

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.SetEndGameList);

        var count = (uint)Entries.Count;

        bitStream.WriteRanged(Math.Min(count, EntryCountMax), EntryCountMax);

        if (count >= EntryCountMax)
        {
            bitStream.Write1();
            InventoryItemRef.WriteUInt32(bitStream, count);
        }

        foreach (var entry in Entries)
        {
            bitStream.WriteString(entry.Name);

            InventoryItemRef.WriteOptionalUInt32(bitStream, entry.ValueA);
            InventoryItemRef.WriteOptionalUInt32(bitStream, entry.ValueB);

            bitStream.WriteRanged(Math.Min(entry.Rank, RankMax), RankMax);

            InventoryItemRef.WriteUInt32(bitStream, (uint)entry.ScoreA);
            InventoryItemRef.WriteUInt32(bitStream, (uint)entry.ScoreB);
        }

        InventoryItemRef.WriteUInt32(bitStream, Unnamed);

        return bitStream;
    }
}
