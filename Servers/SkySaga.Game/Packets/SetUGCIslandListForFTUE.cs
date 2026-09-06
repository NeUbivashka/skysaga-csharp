namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 27, server to client. Reader: FUN_007FFC80 -> FUN_00BB3E50(1, 0x1B, ...).
///
/// A single list, read by FUN_007E5E40:
///
///     count      FUN_007D7510   ranged, max 0x400 -> 11 bits, escape when it reads exactly 1024
///     escape     1 bit, then a full 32-bit count
///     entries    count x FUN_007EA3B0
///
/// FUN_007EA3B0 is the same reader ServerInfo uses for ServerOwnerGUID and the rest: one presence
/// bit, then - only if set - sixteen bytes as four 32-bit words. So this is a list of UUIDs, which
/// fits the name: the islands offered during the FTUE.
///
/// The elements go out through the same helper shape as ServerInfo's UUIDs. Byte order inside a
/// UUID only affects how it prints; what matters for the stream is the width, 129 bits present or
/// 1 bit absent.
///
/// An empty list is a complete, legal message and costs 11 bits.
/// </summary>
public class SetUGCIslandListForFTUE : ISerializablePacket
{
    /// <summary>Above this the count carries an escape bit plus a full 32-bit count.</summary>
    private const uint IslandCountMax = 0x400;

    public List<Guid?> IslandUuids = [];

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.SetUGCIslandListForFTUE);

        var count = (uint)IslandUuids.Count;

        bitStream.WriteRanged(Math.Min(count, IslandCountMax), IslandCountMax);

        if (count >= IslandCountMax)
        {
            bitStream.Write1();

            var countBytes = BitConverter.GetBytes(count);
            Array.Reverse(countBytes);
            bitStream.WriteBits(countBytes, 32, true);
        }

        foreach (var uuid in IslandUuids)
            WriteUuid(bitStream, uuid);

        return bitStream;
    }

    /// <summary>One presence bit, then the 16 bytes as four 32-bit words - same as ServerInfo.</summary>
    private static void WriteUuid(BitStream bitStream, Guid? value)
    {
        if (value is null)
        {
            bitStream.Write0();

            return;
        }

        bitStream.Write1();

        var bytes = value.Value.ToByteArray();

        for (var offset = 0; offset < 16; offset += 4)
            bitStream.WriteBits(BitConverter.GetBytes(BitConverter.ToUInt32(bytes, offset)), 32, true);
    }
}
