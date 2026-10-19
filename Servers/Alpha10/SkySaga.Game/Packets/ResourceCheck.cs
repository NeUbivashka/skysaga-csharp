namespace SkySaga.Game.Packets;

public static class ResourceCheck
{
    private const byte EntryCountMax = 0x10;

    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!ReadBlob(bitStream, out var header) || !bitStream.ReadByte(EntryCountMax, out var shortCount))
            return false;

        uint count = shortCount;

        if (shortCount == EntryCountMax && bitStream.ReadBit() && !bitStream.ReadFullWidthUInt32(out count))
            return false;

        for (var i = 0; i < count; i++)
        {
            ulong time = 0;

            if (bitStream.ReadBit() && !bitStream.ReadUInt64(out time) || !ReadBlob(bitStream, out var data))
                return false;

            Console.WriteLine($"[ResourceCheck] entry {i}: time {time}, {data.Length} bytes: {Convert.ToHexString(data)}");
        }

        Console.WriteLine($"[ResourceCheck] header {Convert.ToHexString(header)}, {count} entries");
        return true;
    }

    private static bool ReadBlob(BitStream bitStream, out byte[] data)
    {
        data = [];

        if (!bitStream.ReadFullWidthUInt32(out var length) || length > bitStream.GetNumberOfUnreadBits() / 8)
            return false;

        data = new byte[length];
        return length == 0 || bitStream.ReadBits(data, length * 8, alignBitsToRight: true);
    }
}
