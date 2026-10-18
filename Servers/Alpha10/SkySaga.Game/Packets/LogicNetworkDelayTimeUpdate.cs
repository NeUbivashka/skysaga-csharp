namespace SkySaga.Game.Packets;

public class LogicNetworkDelayTimeUpdate : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.LogicNetworkDelayTimeUpdate);

        return bitStream;
    }
}
