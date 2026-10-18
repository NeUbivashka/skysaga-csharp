namespace SkySaga.Game.Packets;

public class SetSystemCharacterMap : ISerializablePacket
{
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
