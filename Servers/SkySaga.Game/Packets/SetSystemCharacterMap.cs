namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 28, server to client. Reader: FUN_007FF910 -> FUN_00BB3E50(1, 0x1C, ...), body read by
/// FUN_007D8390.
///
///     count    FUN_007D7140: ranged max 8 -> 4 bits, escape at 8 (1 bit + 32 bits)
///     keys     count x string        (read by FUN_007E7680)
///     values   count x (string, string)
///
/// Three strings per entry, and the grouping is not interleaved: every key is read first, then the
/// pairs. That ordering was the open question when this packet was first deferred, and it is
/// settled by FUN_0080BFE0 - it pre-sizes the second container to the key count before the loop,
/// so FUN_0080C280 returns a constant and the loop runs exactly count times rather than chasing a
/// container that grows underneath it.
/// </summary>
public class SetSystemCharacterMap : ISerializablePacket
{
    /// <summary>Above this the count carries an escape bit plus a full 32-bit count.</summary>
    private const uint EntryCountMax = 8;

    public List<Entry> Entries = [];

    public class Entry
    {
        public string? Key;
        public string? ValueA;
        public string? ValueB;
    }

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.SetSystemCharacterMap);

        var count = (uint)Entries.Count;

        bitStream.WriteRanged(Math.Min(count, EntryCountMax), EntryCountMax);

        if (count >= EntryCountMax)
        {
            bitStream.Write1();
            InventoryItemRef.WriteUInt32(bitStream, count);
        }

        foreach (var entry in Entries)
            bitStream.WriteString(entry.Key);

        foreach (var entry in Entries)
        {
            bitStream.WriteString(entry.ValueA);
            bitStream.WriteString(entry.ValueB);
        }

        return bitStream;
    }
}
