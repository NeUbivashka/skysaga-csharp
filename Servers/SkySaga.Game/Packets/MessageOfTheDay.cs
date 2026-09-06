namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 151, server to client. Reader: FUN_009E5560 -> FUN_00BB3E50(1, 0x97, ...).
///
///     Title     string
///     (FUN_00CFA920 reads nothing - it calls FUN_00EA7A60, which only zeroes two words)
///     Lines     FUN_009E53C0: count via FUN_009E4870, ranged max 0x1A -> 5 bits, escape at 26;
///               then per entry an 8-bit code (max 0x19, clamped) and a string
///     Body      string
///     Footer    string
///     Link      string
///     Version   32 bits, full width -> big-endian
///
/// The line codes are clamped to 0..0x19 by the client, and zero is treated specially - the reader
/// writes 0 through unchanged and anything else as-is, so 0 is likely "plain". Names are by role.
/// </summary>
public class MessageOfTheDay : ISerializablePacket
{
    /// <summary>Above this the count carries an escape bit plus a full 32-bit count.</summary>
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
