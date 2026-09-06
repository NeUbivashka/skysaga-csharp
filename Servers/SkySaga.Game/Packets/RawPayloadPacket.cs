namespace SkySaga.Game.Packets;

/// <summary>
/// Sends a packet id followed by a verbatim block of payload bits.
///
/// This exists because the shape of the animation-sequence replies is not known yet. The one
/// thing we can observe is the client's own request, so the safest reply is to hand the same
/// bits back rather than guess a field layout - guessing also drags in the byte-order question
/// (the captured EntityId reads big-endian, while other writers here reverse manually), and
/// echoing sidesteps it entirely.
///
/// Replace this with a real typed packet as soon as the layout is actually known.
/// </summary>
public class RawPayloadPacket : ISerializablePacket
{
    public required PacketId PacketId;
    public required byte[] Payload;
    public required uint PayloadBits;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId);

        if (PayloadBits > 0)
            bitStream.WriteBits(Payload, PayloadBits, true);

        return bitStream;
    }
}
