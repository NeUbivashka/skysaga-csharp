namespace SkySaga.Game.Packets;

public class PlayerKicked : ISerializablePacket
{
    private const uint ReasonMax = 2;
    public uint Reason;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.PlayerKicked);

        bitStream.WriteRanged(Math.Min(Reason, ReasonMax), ReasonMax);

        return bitStream;
    }
}
