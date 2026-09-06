using System.Linq;

namespace SkySaga.Game.Packets;

/// <summary>
/// Handles ordinal 10 (RPCResourceCheck) arriving FROM the client.
///
/// This used to be sent by the server, which was backwards. The client has no receive stub for
/// ordinal 10 - only a sender, FUN_008260D0, which opens an outgoing RPC via
/// FUN_00BC56B0(10, ...) and then writes a 32-bit length followed by that many bytes. Everything
/// the server sent under this id was dropped by the dispatcher without a word.
///
/// So the direction is client to server, and this is the missing half. It logs the payload and
/// nothing more: the length prefix is readable, but what the bytes are - a manifest, a set of
/// hashes, a file list - is not established, and inventing a reply for a request we cannot read
/// is how the 271 crash happened.
///
/// If this never fires, that is also worth knowing. Across every session logged so far the client
/// has only ever sent ordinals 3, 4, 5, 6, 29 and 30, so ResourceCheck may simply not be used in
/// this flow.
/// </summary>
public static class ResourceCheckReceived
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        var totalBits = bitStream.GetNumberOfBitsUsed();

        // Ordinal 10 is below 121, so the id took a single byte on the wire.
        var remainingBits = (uint)Math.Max(0, totalBits - 8);
        var payload = new byte[(remainingBits + 7) / 8];

        var readOk = remainingBits == 0 || bitStream.ReadBits(payload, remainingBits, alignBitsToRight: true);

        var length = payload.Length >= 4
            ? (payload[0] << 24) | (payload[1] << 16) | (payload[2] << 8) | payload[3]
            : -1;

        Console.WriteLine($"[ResourceCheck] Received (ordinal 10) from guid={connection.Guid.g:X}. "
            + $"totalBits={totalBits} payloadBits={remainingBits} readOk={readOk} "
            + $"lengthPrefix={length} raw: {string.Join(" ", payload.Take(64).Select(b => b.ToString("x2")))}");

        return true;
    }
}
