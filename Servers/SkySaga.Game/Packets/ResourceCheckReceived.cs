using System.Linq;

namespace SkySaga.Game.Packets;

public static class ResourceCheckReceived
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        var totalBits = bitStream.GetNumberOfBitsUsed();

        var remainingBits = (uint)Math.Max(0, totalBits - 8);
        var payload = new byte[(remainingBits + 7) / 8];

        var readOk = remainingBits == 0 || bitStream.ReadBits(payload, remainingBits, alignBitsToRight: true);

        var length = payload.Length >= 4
            ? (payload[0] << 24) | (payload[1] << 16) | (payload[2] << 8) | payload[3]
            : -1;

        Console.WriteLine($"[ResourceCheck] Received (ordinal 10) from guid={connection.Guid.g:X}. "
            + $"totalBits={totalBits} payloadBits={remainingBits} readOk={readOk} "
            + $"lengthPrefix={length} raw: {string.Join(" ", payload.Take(64).Select(b => b.ToString("x2")))}");

        return true;
    }
}
