using System.Linq;

namespace SkySaga.Game.Packets;

/// <summary>
/// Handles ordinal 271 (RPCAnimationSequenceCompleted) arriving FROM the client.
///
/// Observed on the wire as 'ff 96 00 00 00 01 48' - escape byte 0x96 plus the 121 bias gives 271,
/// and the payload is the same (entity id + sequence index) pair the client used to request the
/// sequence. So 271 is the client reporting that a sequence it started has finished, not a server
/// command as first assumed.
///
/// Until this handler existed it was logged as an unhandled packet and dropped.
/// </summary>
public static class AnimationSequenceCompletedReceived
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        var totalBits = bitStream.GetNumberOfBitsUsed();

        // 271 is above the escape threshold, so the id took two bytes.
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
