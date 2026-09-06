namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 282, server to client. Reader: FUN_007F8060 -> FUN_00BB3E50(1, 0x11A, ...).
///
/// One 16-bit index, read by FUN_007D8010 and handed to FUN_007DF760 for lookup - the same shape
/// AnimationSequenceStarting uses for its sequence index.
/// </summary>
public class NarrativeDidNotComplete : ISerializablePacket
{
    public ushort NarrativeIndex;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.NarrativeDidNotComplete);

        bitStream.WriteBits(BitConverter.GetBytes(NarrativeIndex), 16, true);

        return bitStream;
    }
}
