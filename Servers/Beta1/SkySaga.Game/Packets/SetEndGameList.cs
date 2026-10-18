namespace SkySaga.Game.Packets;

public class SetEndGameList : ISerializablePacket
{
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
