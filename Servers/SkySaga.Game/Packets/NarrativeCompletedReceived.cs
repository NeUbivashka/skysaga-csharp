using System.Linq;

namespace SkySaga.Game.Packets;

public static class NarrativeCompletedReceived
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        var totalBits = bitStream.GetNumberOfBitsUsed();

        var remainingBits = (uint)Math.Max(0, totalBits - 8);
        var payload = new byte[(remainingBits + 7) / 8];

        var readOk = remainingBits == 0 || bitStream.ReadBits(payload, remainingBits, alignBitsToRight: true);
        var hex = string.Join(" ", payload.Select(b => b.ToString("x2")));

        Console.WriteLine($"[NarrativeCompleted] Received (ordinal 105) from the client. "
            + $"remainingBitsAfterId={remainingBits} readOk={readOk} payload: {hex}");

        return true;
    }
}
