namespace SkySaga.Game.Packets;

public class SyncLogicNetwork : ISerializablePacket
{
    private const uint StateCountMax = 8;

    public ulong Timestamp;

    public int NetworkId;

    public List<ulong> States = [];

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.SyncLogicNetwork);

        PvPWaitingInfo.WriteUInt64(bitStream, Timestamp);

        InventoryItemRef.WriteUInt32(bitStream, (uint)NetworkId);

        var count = (uint)States.Count;

        bitStream.WriteRanged(Math.Min(count, StateCountMax), StateCountMax);

        if (count >= StateCountMax)
        {
            bitStream.Write1();
            InventoryItemRef.WriteUInt32(bitStream, count);
        }

        foreach (var state in States)
            PvPWaitingInfo.WriteUInt64(bitStream, state);

        return bitStream;
    }
}
