namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 328, server to client. Reader: FUN_007F4600 -> FUN_00BB3E50(1, 0x148, ...).
///
/// The whole body is the structure FUN_007E9570 reads:
///
///     QueueId      1 presence bit + 32 bits
///     InviterUuid  1 presence bit + 4 x 32 bits
///     InviteeUuid  1 presence bit + 4 x 32 bits
///     FlagA        1 bit
///     FlagB        1 bit
///     Timestamp    FUN_00C715B0: 64 bits -> big-endian
///     FlagC        1 bit
///     Reason       ranged, max 7 -> 3 bits
///     FlagD        1 bit
///
/// The four single bits are packed into one byte in the client's structure, at bit positions 0
/// through 3 in the order read - they are separate fields on the wire, not a bitfield.
/// </summary>
public class GameQueueInviteFailed : ISerializablePacket
{
    private const uint ReasonMax = 7;

    public uint? QueueId;
    public Guid? InviterUuid;
    public Guid? InviteeUuid;
    public bool FlagA;
    public bool FlagB;
    public ulong Timestamp;
    public bool FlagC;
    public uint Reason;
    public bool FlagD;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.GameQueueInviteFailed);

        InventoryItemRef.WriteOptionalUInt32(bitStream, QueueId);
        InventoryItemRef.WriteOptionalUuid(bitStream, InviterUuid);
        InventoryItemRef.WriteOptionalUuid(bitStream, InviteeUuid);

        bitStream.Write(FlagA);
        bitStream.Write(FlagB);

        PvPWaitingInfo.WriteUInt64(bitStream, Timestamp);

        bitStream.Write(FlagC);

        bitStream.WriteRanged(Math.Min(Reason, ReasonMax), ReasonMax);

        bitStream.Write(FlagD);

        return bitStream;
    }
}
