using System.Linq;

namespace SkySaga.Game.Packets;

public static class AnimationSequenceCompletedReceived
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        var totalBits = bitStream.GetNumberOfBitsUsed();

        var remainingBits = (uint)Math.Max(0, totalBits - 16);
        var payload = new byte[(remainingBits + 7) / 8];

        var readOk = remainingBits == 0 || bitStream.ReadBits(payload, remainingBits, alignBitsToRight: true);
        var hex = string.Join(" ", payload.Select(b => b.ToString("x2")));

        var entityId = payload.Length >= 4
            ? (payload[0] << 24) | (payload[1] << 16) | (payload[2] << 8) | payload[3]
            : -1;
        var trailer = payload.Length >= 5 ? payload[4] : (byte)0;

        Console.WriteLine($"[AnimationSequenceCompleted] Received (ordinal 271) from the client. "
            + $"EntityId={entityId} trailer=0x{trailer:x2} (sequence index {trailer >> 3}) "
            + $"readOk={readOk} payload: {hex}");

        return true;
    }
}
