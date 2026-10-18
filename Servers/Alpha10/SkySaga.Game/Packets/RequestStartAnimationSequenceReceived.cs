using System.Linq;

namespace SkySaga.Game.Packets;

public static class RequestStartAnimationSequenceReceived
{
    private static readonly bool SendFadeComplete = true;

    private static readonly bool SendCompleted = false;

    private static readonly bool SendProgressData = false;

    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        var totalBits = bitStream.GetNumberOfBitsUsed();
        var totalBytes = bitStream.GetNumberOfBytesUsed();

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
