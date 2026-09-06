using System.Linq;

namespace SkySaga.Game.Packets;

/// <summary>
/// Handles ordinal 268 (RPCRequestStartAnimationSequence) from the client.
///
/// What two captures now confirm:
///
///     EntityId=49  ->  payload 00 00 00 31 38
///     EntityId=4   ->  payload 00 00 00 04 38
///
/// 40 bits: a 32-bit entity id (big-endian on the wire) followed by one byte, 0x38, which did not
/// change between runs. There is no string anywhere in it. That matters, because every class in
/// this family (AnimationSequenceStarting, SetAnimationSequenceProgressData,
/// AnimationSequenceCompleted) was written around a SequenceName string - those shapes are
/// unverified guesses, and the first reply built from them was almost certainly unparseable to the
/// client, which fits the client going silent right after receiving it.
///
/// So this version stops guessing field layouts and echoes the client's own payload bits back.
/// That also sidesteps byte order: the captured id reads big-endian while other writers in this
/// project reverse their bytes by hand, and an echo cannot get that wrong.
///
/// Directions are now settled by observation, and my earlier guess was wrong in two places:
///
///     client -> 268 RequestStartAnimationSequence   (entity id + sequence index)
///     server -> 272 AnimationSequenceStarting       (echo - this is what started the voice-over)
///     server -> 269 AnimationSequenceFadeComplete   <-- server to client, never sent until now
///     client -> 271 AnimationSequenceCompleted      (observed arriving as 'ff 96 ...')
///     client -> 105 NarrativeCompleted              (observed, three in a row)
///
/// 269 is not something the client reports - it never sends one. It is the server telling the
/// client the fade is done, which is exactly the stuck step. The first attempt did send it, but
/// as an empty packet, and this family is keyed by entity id, so the client had nothing to match
/// it against and dropped it. It now goes out echoing the same payload as 272.
///
/// The trailing byte is a sequence index in the top 5 bits: 0x38, 0x40, 0x48 across consecutive
/// steps is 7, 8, 9 shifted left by 3.
/// </summary>
public static class RequestStartAnimationSequenceReceived
{
    /// <summary>
    /// Send 269 AnimationSequenceFadeComplete after 272, echoing the same payload. On by default
    /// now: the client never sends a 269 of its own, so nothing else can be producing it, and the
    /// fade staying black is the symptom of it never arriving.
    /// Not const: a const bool folds the branch away and trips CS0162.
    /// </summary>
    private static readonly bool SendFadeComplete = true;

    /// <summary>
    /// Send 271 AnimationSequenceCompleted, echoing the same payload.
    ///
    /// DISABLED - this one crashed the client.
    ///
    /// Sending 271 with the echoed (entity id + sequence index) payload produced
    /// STATUS_FLOAT_MULTIPLE_TRAPS (0xC00002B5) at 0x00D45EDC in the client. That is a real
    /// result, not just a failure: it proves 271 IS handled in the server-to-client direction,
    /// and that its handler parses at least one float out of the payload. Our 40 echoed bits get
    /// read as a float, land on a denormal or an invalid value, and trap.
    ///
    /// So echoing is the right technique for 272 but the wrong one for 271 - this packet has a
    /// different, float-bearing layout. Do not turn this back on until that layout is read out of
    /// the handler at 0x00D45EDC.
    /// </summary>
    private static readonly bool SendCompleted = false;

    /// <summary>
    /// Also send 270 SetAnimationSequenceProgressData. Off: 270 has never been observed in either
    /// direction, so there is no evidence about its payload beyond the family's entity-id key.
    /// Leave this for last, and change one flag at a time - flipping several at once makes it
    /// impossible to tell which one moved the client.
    /// </summary>
    private static readonly bool SendProgressData = false;

    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        var totalBits = bitStream.GetNumberOfBitsUsed();
        var totalBytes = bitStream.GetNumberOfBytesUsed();

        // The id occupied two bytes on the wire (0xFF escape + one byte).
        var remainingBits = (uint)Math.Max(0, totalBits - 16);
        var payload = new byte[(remainingBits + 7) / 8];

        var readOk = remainingBits == 0 || bitStream.ReadBits(payload, remainingBits, alignBitsToRight: true);
        var hex = string.Join(" ", payload.Select(b => b.ToString("x2")));

        var entityId = payload.Length >= 4
            ? (payload[0] << 24) | (payload[1] << 16) | (payload[2] << 8) | payload[3]
            : -1;
        var trailer = payload.Length >= 5 ? payload[4] : (byte)0;

        var expectedId = connection.PlayerEntity.Id;
        var idNote = entityId == expectedId
            ? "matches the player entity"
            : $"does NOT match the player entity ({expectedId})";

        Console.WriteLine($"[RequestStartAnimationSequence] Received (ordinal 268). "
            + $"totalBytes={totalBytes} totalBits={totalBits} remainingBitsAfterId={remainingBits} "
            + $"readOk={readOk} payload: {hex}");
        Console.WriteLine($"[RequestStartAnimationSequence] Parsed EntityId={entityId} ({idNote}), "
            + $"trailer=0x{trailer:x2} (sequence index {trailer >> 3}).");

        connection.Send(new RawPayloadPacket
        {
            PacketId = PacketId.AnimationSequenceStarting,
            Payload = payload,
            PayloadBits = remainingBits
        });

        Console.WriteLine($"[RequestStartAnimationSequence] Sent AnimationSequenceStarting (ordinal 272), "
            + $"echoing {remainingBits} payload bits.");

        if (SendFadeComplete)
        {
            connection.Send(new RawPayloadPacket
            {
                PacketId = PacketId.AnimationSequenceFadeComplete,
                Payload = payload,
                PayloadBits = remainingBits
            });

            Console.WriteLine("[RequestStartAnimationSequence] Sent AnimationSequenceFadeComplete (ordinal 269), "
                + "echoing the same payload.");
        }

        if (SendProgressData)
        {
            connection.Send(new RawPayloadPacket
            {
                PacketId = PacketId.SetAnimationSequenceProgressData,
                Payload = payload,
                PayloadBits = remainingBits
            });

            Console.WriteLine("[RequestStartAnimationSequence] Sent SetAnimationSequenceProgressData (ordinal 270).");
        }

        if (SendCompleted)
        {
            connection.Send(new RawPayloadPacket
            {
                PacketId = PacketId.AnimationSequenceCompleted,
                Payload = payload,
                PayloadBits = remainingBits
            });

            Console.WriteLine("[RequestStartAnimationSequence] Sent AnimationSequenceCompleted (ordinal 271), "
                + "echoing the same payload.");
        }

        return true;
    }
}
