namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 140, server to client. Reader: FUN_008016F0 -> FUN_00BB3E50(1, 0x8C, ...).
///
///     WorldUuid    1 presence bit + 4 x 32 bits
///     ServerUuid   1 presence bit + 4 x 32 bits
///     Host         string
///     Port         32 bits, full width -> big-endian
///     Token        FUN_007D6E60: a 32-bit big-endian length, then that many bytes
///
/// The last field is a length-prefixed blob, not a string: FUN_007D6E60 reads a 32-bit count,
/// allocates, reads count * 8 bits, and hands the buffer to FUN_007DF4E0. That is a different
/// shape from WriteString, which uses a one-bit small/large flag and an 8-bit length, so it needs
/// its own writer.
///
/// This is the packet that moves a client to another server, which is what a teleport ultimately
/// is. Names are by role - two identities, an address, a port and an opaque credential is the
/// obvious reading, but nothing here names itself.
/// </summary>
public class TransferToServer : ISerializablePacket
{
    public Guid? WorldUuid;

    public Guid? ServerUuid;

    public string? Host;

    public int Port;

    public byte[] Token = [];

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.TransferToServer);

        InventoryItemRef.WriteOptionalUuid(bitStream, WorldUuid);
        InventoryItemRef.WriteOptionalUuid(bitStream, ServerUuid);

        bitStream.WriteString(Host);

        InventoryItemRef.WriteUInt32(bitStream, (uint)Port);

        WriteBlob(bitStream, Token);

        return bitStream;
    }

    /// <summary>A 32-bit big-endian length, then the bytes. Same shape ChunkSync uses.</summary>
    internal static void WriteBlob(BitStream bitStream, byte[] data)
    {
        var lengthBytes = BitConverter.GetBytes(data.Length);

        Array.Reverse(lengthBytes);

        bitStream.WriteBits(lengthBytes, 32, true);

        if (data.Length > 0)
            bitStream.WriteBits(data, (uint)data.Length * 8, true);
    }
}
