namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 38, server to client. Reader: FUN_007F5A60 -> FUN_00BB3E50(1, 0x26, ...).
///
///     TargetEntityId   32 bits, full width -> big-endian
///     SourceEntityId   32 bits, full width -> big-endian
///     Amount           FUN_007D69B0: ranged, max 0x7FFF -> 15 bits
///     Kind             ranged, max 9 -> 4 bits
///     Unnamed          32 bits, full width -> big-endian
///
/// Five fields, in that order. The widths come from the reader; the names are by role, since the
/// logger's name pointers are lost to the packed thunks in this stub. Two entity ids followed by
/// an amount is the obvious reading of a heal event, but treat the names as provisional and the
/// layout as solid.
/// </summary>
public class HealOccurred : ISerializablePacket
{
    private const uint AmountMax = 0x7FFF;
    private const uint KindMax = 9;

    public int TargetEntityId;

    public int SourceEntityId;

    public uint Amount;

    public uint Kind;

    public int Unnamed;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.HealOccurred);

        WriteInt32(bitStream, TargetEntityId);
        WriteInt32(bitStream, SourceEntityId);

        bitStream.WriteRanged(Math.Min(Amount, AmountMax), AmountMax);
        bitStream.WriteRanged(Math.Min(Kind, KindMax), KindMax);

        WriteInt32(bitStream, Unnamed);

        return bitStream;
    }

    private static void WriteInt32(BitStream bitStream, int value)
    {
        var bytes = BitConverter.GetBytes(value);

        Array.Reverse(bytes);

        bitStream.WriteBits(bytes, 32, true);
    }
}
