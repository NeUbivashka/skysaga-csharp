using System.Linq;

namespace SkySaga.Game.Packets;

/// <summary>
/// Handles ordinal 105 (RPCNarrativeCompleted) arriving FROM the client.
///
/// Observed as 'ef 49 7c c0 00 00 00 10' - a direct (non-escaped) id byte 0xEF, so 239 - 134 = 105.
/// Three arrived back to back, which is why the payload is worth logging before deciding what, if
/// anything, the server owes in reply: two of the three were byte-identical and one differed in a
/// single byte (0x49 vs 0x4a), so there is some per-narrative discriminator in there.
///
/// Nothing is sent back yet. There is no evidence about what a reply should contain, and the
/// packet was being dropped as unhandled before this, so simply seeing it decoded is the point.
/// </summary>
public static class NarrativeCompletedReceived
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        var totalBits = bitStream.GetNumberOfBitsUsed();

        // 105 is below the escape threshold, so its id was a single byte.
        var remainingBits = (uint)Math.Max(0, totalBits - 8);
        var payload = new byte[(remainingBits + 7) / 8];

        var readOk = remainingBits == 0 || bitStream.ReadBits(payload, remainingBits, alignBitsToRight: true);
        var hex = string.Join(" ", payload.Select(b => b.ToString("x2")));

        Console.WriteLine($"[NarrativeCompleted] Received (ordinal 105) from the client. "
            + $"remainingBitsAfterId={remainingBits} readOk={readOk} payload: {hex}");

        return true;
    }
}
