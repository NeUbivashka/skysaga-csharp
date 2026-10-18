namespace SkySaga.Game.Packets;

public class AddLogicNetworkConnection : ISerializablePacket
{
    private const uint ReasonMax = 3;

    public LogicNetworkConnection Connection = new();

    public uint Reason;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.AddLogicNetworkConnection);

        Connection.Encode(bitStream);

        bitStream.WriteRanged(Math.Min(Reason, ReasonMax), ReasonMax);

        return bitStream;
    }
}
