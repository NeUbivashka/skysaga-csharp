namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 287, server to client. Reader: FUN_00DBE1B0 -> FUN_00BB3E50(1, 0x11F, ...).
///
///     Payload    FUN_007D6E60: a 32-bit big-endian length, then that many bytes
///     Channel    FUN_00DBCD90: ranged, max 7 -> 3 bits
///     Sequence   FUN_00DBCE20: 1 escape bit, then 16 bits, or 32 if the bit is set
///     Flag       1 bit
///
/// The stub lives among the network plumbing (0x00DBxxxx) rather than with the ordinary packet
/// readers, which is why it looked like a container rather than a packet. It is a packet: the
/// batched contents ride in Payload as an opaque blob, using the same length-prefixed shape
/// TransferToServer uses for its token, and the three fields after it are ordinary.
///
/// Nothing here says how the blob is framed internally - whether it is a run of complete RPCs or
/// something else. That is a separate question from the wire layout, which is settled.
/// </summary>
public class Batch : ISerializablePacket
{
    private const uint ChannelMax = 7;

    /// <summary>Above this the sequence needs the 32-bit form.</summary>
    private const uint SequenceShortMax = 0xFFFF;

    public byte[] Payload = [];

    public uint Channel;

    public uint Sequence;

    public bool Flag;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.Batch);

        TransferToServer.WriteBlob(bitStream, Payload);

        bitStream.WriteRanged(Math.Min(Channel, ChannelMax), ChannelMax);

        if (Sequence <= SequenceShortMax)
        {
            bitStream.Write0();
            bitStream.WriteBits(BitConverter.GetBytes((ushort)Sequence), 16, true);
        }
        else
        {
            bitStream.Write1();
            InventoryItemRef.WriteUInt32(bitStream, Sequence);
        }

        bitStream.Write(Flag);

        return bitStream;
    }
}
