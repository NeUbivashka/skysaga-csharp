namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 48, server to client. Reader: FUN_007EE9C0 -> FUN_00BB3E50(1, 0x30, ...).
///
/// One ranged field, maximum 0x98, so 8 bits. That is the whole packet.
///
/// The stub also calls FUN_0077A510 before it, which looks like a field read and is not: neither
/// that function nor any of its five callees touches the bitstream. It builds a temporary.
/// </summary>
public class CombatResult : ISerializablePacket
{
    private const uint ResultMax = 0x98;

    public uint Result;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.CombatResult);

        bitStream.WriteRanged(Math.Min(Result, ResultMax), ResultMax);

        return bitStream;
    }
}
