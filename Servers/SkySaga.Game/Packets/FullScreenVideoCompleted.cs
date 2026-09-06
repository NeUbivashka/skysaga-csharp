using System.Linq;

namespace SkySaga.Game.Packets;

public static class FullScreenVideoCompleted
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        var totalBits = bitStream.GetNumberOfBitsUsed();
        var totalBytes = bitStream.GetNumberOfBytesUsed();

        var remainingBits = (uint)(totalBits - 8);
        var buffer = new byte[(remainingBits + 7) / 8];

        var readOk = remainingBits == 0 || bitStream.ReadBits(buffer, remainingBits, alignBitsToRight: true);

        var hex = string.Join(" ", buffer.Select(b => b.ToString("x2")));

        Console.WriteLine($"[FullScreenVideoCompleted] Received. totalBytes={totalBytes} totalBits={totalBits} "
            + $"remainingBitsAfterId={remainingBits} readOk={readOk} raw: {hex} (was playing: {connection.CurrentVideoFileName})");

        connection.CurrentVideoFileName = null;

        return true;
    }
}
