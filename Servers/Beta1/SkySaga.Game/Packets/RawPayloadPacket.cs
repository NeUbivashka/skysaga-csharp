namespace SkySaga.Game.Packets;

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
