namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 272, server to client. Reader: FUN_007EB7E0 -> FUN_00BB3E50(1, 0x110, ...).
///
///     EntityId        32 bits, full width -> big-endian
///     SequenceIndex   FUN_007D79F0: 16 bits, then FUN_007DF5E0 looks the sequence up by it
///
/// 48 bits of body. THE SERVER HAS BEEN SENDING 40.
///
/// Until now this went out as a raw echo of the client's ordinal-268 payload, which is five bytes:
/// a 32-bit entity id and one trailing byte (0x38, the sequence index in its top five bits). That
/// is 268's encoding, not 272's. The client reads a 16-bit index here, so it consumed the 0x38
/// plus eight bits that were not in the packet.
///
/// Reading past the end is exactly the failure mode behind the STATUS_FLOAT_MULTIPLE_TRAPS on
/// ordinal 271 - the same family of packets, the same habit of echoing a payload shaped for a
/// different ordinal. Send this typed instead.
///
/// SequenceIndex is the plain index here (7 for Eva_Leads_Player), not the shifted byte 268 uses.
/// That follows from the reader handing the value straight to a lookup, but it is the one part of
/// this that the layout alone does not prove.
/// </summary>
public class AnimationSequenceStarting : ISerializablePacket
{
    public int EntityId;

    public ushort SequenceIndex;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.AnimationSequenceStarting);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);
        bitStream.WriteBits(BitConverter.GetBytes(SequenceIndex), 16, true);

        return bitStream;
    }
}
