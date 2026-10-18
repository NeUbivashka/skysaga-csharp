namespace SkySaga.Game.Packets;

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
