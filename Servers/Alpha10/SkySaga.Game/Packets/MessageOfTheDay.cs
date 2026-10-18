namespace SkySaga.Game.Packets;

public class MessageOfTheDay : ISerializablePacket
{
    private const uint LineCountMax = 0x1A;

    private const uint LineKindMax = 0x19;

    public string? Title;

    public List<Line> Lines = [];

    public string? Body;
    public string? Footer;
    public string? Link;
    public uint Version;

    public class Line
    {
        public uint Kind;
        public string? Text;
    }

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.MessageOfTheDay);

        bitStream.WriteString(Title);

        var count = (uint)Lines.Count;

        bitStream.WriteRanged(Math.Min(count, LineCountMax), LineCountMax);

        if (count >= LineCountMax)
        {
            bitStream.Write1();
            InventoryItemRef.WriteUInt32(bitStream, count);
        }

        foreach (var line in Lines)
        {
            bitStream.WriteBits([(byte)Math.Min(line.Kind, LineKindMax)], 8, true);
            bitStream.WriteString(line.Text);
        }

        bitStream.WriteString(Body);
        bitStream.WriteString(Footer);
        bitStream.WriteString(Link);

        InventoryItemRef.WriteUInt32(bitStream, Version);

        return bitStream;
    }
}
