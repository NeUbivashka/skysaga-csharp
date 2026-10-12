using System.Linq;

namespace SkySaga.Game.Packets;

public static class RunActionListReceived
{
    private static readonly bool EchoToRequester = false;

    private static readonly bool StartAnimationSequence = false;

    private const byte SequenceIndexByte = 0x38;

    private static readonly bool BroadcastToOthers = false;

    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        var totalBits = bitStream.GetNumberOfBitsUsed();

        var remainingBits = (uint)Math.Max(0, totalBits - 8);
        var payload = new byte[(remainingBits + 7) / 8];

        var readOk = remainingBits == 0 || bitStream.ReadBits(payload, remainingBits, alignBitsToRight: true);

        var hex = string.Join(" ", payload.Select(b => b.ToString("x2")));

        var actionListId = payload.Length >= 4
            ? (payload[0] << 24) | (payload[1] << 16) | (payload[2] << 8) | payload[3]
            : -1;

        var entityId = payload.Length >= 8
            ? (payload[4] << 24) | (payload[5] << 16) | (payload[6] << 8) | payload[7]
            : -1;

        var expectedId = connection.PlayerEntity.Id;
        var idNote = entityId == expectedId
            ? "the player entity"
            : $"NOT the player entity ({expectedId})";

        Console.WriteLine($"[RunActionList] Received (ordinal 29). totalBits={totalBits} "
            + $"payloadBits={remainingBits} readOk={readOk} raw: {hex}");
        Console.WriteLine($"[RunActionList] ActionListId={actionListId} EntityId={entityId} ({idNote}).");

        if (!readOk)
        {
            Console.WriteLine("[RunActionList] Payload read FAILED - not echoing.");

            return true;
        }

        if (EchoToRequester)
        {
            connection.Send(new RawPayloadPacket
            {
                PacketId = PacketId.RunActionList,
                Payload = payload,
                PayloadBits = remainingBits
            });

            Console.WriteLine($"[RunActionList] Echoed ordinal 29 back to the requester, "
                + $"{remainingBits} payload bits.");
        }

        if (StartAnimationSequence && entityId > 0)
        {
            var sequencePayload = new byte[]
            {
                (byte)(entityId >> 24),
                (byte)(entityId >> 16),
                (byte)(entityId >> 8),
                (byte)entityId,
                SequenceIndexByte
            };

            connection.Send(new RawPayloadPacket
            {
                PacketId = PacketId.AnimationSequenceStarting,
                Payload = sequencePayload,
                PayloadBits = (uint)sequencePayload.Length * 8
            });

            Console.WriteLine($"[RunActionList] Sent AnimationSequenceStarting (ordinal 272) "
                + $"unprompted for EntityId={entityId}, sequence index {SequenceIndexByte >> 3}, "
                + $"payload {string.Join(" ", sequencePayload.Select(b => b.ToString("x2")))}.");
        }

        if (BroadcastToOthers)
        {
            connection.PlayerConnectionManager.BroadcastToAllExcept(connection.Guid.g, new RawPayloadPacket
            {
                PacketId = PacketId.RunActionList,
                Payload = payload,
                PayloadBits = remainingBits
            });

            Console.WriteLine("[RunActionList] Forwarded ordinal 29 to the other connections.");
        }

        return true;
    }
}
