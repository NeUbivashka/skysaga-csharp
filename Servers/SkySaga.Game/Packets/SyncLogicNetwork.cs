namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 333, server to client. Reader: FUN_00800AF0 -> FUN_00BB3E50(1, 0x14D, ...).
///
///     Timestamp   FUN_00C715B0: 64 bits -> big-endian
///     NetworkId   32 bits, full width -> big-endian
///     States      FUN_007E7B20: count ranged max 8 (escape at 8), then that many 64-bit values
///
/// The state list is the only place so far where a list carries 64-bit elements - FUN_007E7B20
/// reads each with the same 0x40-bit, eight-byte-swap pattern PvPWaitingInfo uses for its single
/// value.
/// </summary>
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
