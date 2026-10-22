using System.Linq;
using System.Buffers.Binary;

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

        // The first 16 payload bits are the client narrative's wire index.
        // The City arrival dump showed eight consecutive values here, one for
        // each narrative in FTUECOFL_ARRIVE_AIRSHIP.
        var narrativeIndex = payload.Length >= 2
            ? BinaryPrimitives.ReadUInt16LittleEndian(payload)
            : (ushort)0;

        if (payload.Length >= 2 && connection.CompletedNarrativeIndices.Add(narrativeIndex))
            connection.SaveState();

        Console.WriteLine($"[NarrativeCompleted] Received (ordinal 105) from the client. "
            + $"index={narrativeIndex} remainingBitsAfterId={remainingBits} readOk={readOk} payload: {hex}");

        return true;
    }
}
