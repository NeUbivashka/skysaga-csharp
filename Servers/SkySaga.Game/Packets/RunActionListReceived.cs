using System.Linq;

namespace SkySaga.Game.Packets;

public static class RunActionListReceived
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        var totalBits = bitStream.GetNumberOfBitsUsed();
        var remainingBits = (uint)Math.Max(0, totalBits - 8);
        var buffer = new byte[(remainingBits + 7) / 8];

        var readOk = remainingBits == 0 || bitStream.ReadBits(buffer, remainingBits, alignBitsToRight: true);

        var hex = string.Join(" ", buffer.Select(b => b.ToString("x2")));

        Console.WriteLine($"[RunActionListReceived] (real ordinal 29) totalBits={totalBits} readOk={readOk} raw: {hex}");

        return true;
    }
}
