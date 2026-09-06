namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 331, server to client. Reader: FUN_007FC620 -> FUN_00BB3E50(1, 0x14B, ...).
///
///     Connection   one LogicNetworkConnection (FUN_007E96F0)
///     Reason       ranged, max 3 -> 2 bits
/// </summary>
public class RemoveLogicNetworkConnection : ISerializablePacket
{
    private const uint ReasonMax = 3;

    public LogicNetworkConnection Connection = new();

    public uint Reason;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.RemoveLogicNetworkConnection);

        Connection.Encode(bitStream);

        bitStream.WriteRanged(Math.Min(Reason, ReasonMax), ReasonMax);

        return bitStream;
    }
}
