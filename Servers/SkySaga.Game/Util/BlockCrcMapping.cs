namespace SkySaga.Game;

public static class BlockCrcMapping
{
    private static readonly Dictionary<uint, int> CrcToBlockId = new()
    {
        { 0x27DAD773, 1 },
        { 0x1E9A8A87, 2 },
        { 0xF55F40E8, 5 },
        { 0x00000000, 7 },
        { 0xEA4DD1DA, 8 },
        { 0xC9205DC3, 10 },
        { 0x0F82C982, 11 },
        { 0x31F94259, 12 },
        { 0x2F39A747, 13 },
        { 0xF0BEBD83, 14 },
        { 0x51A5421B, 24 },
        { 0x6E957590, 29 },
        { 0xCB5EECE6, 35 },
        { 0xCF8D9267, 36 },
        { 0x8AF2B179, 37 },
    };



    public static int? GetBlockId(uint crc)
    {
        if (!CrcToBlockId.TryGetValue(crc, out var blockId))
        {
            return null;
        }
        return blockId;
    }
}
