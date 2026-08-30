namespace SkySaga.Game.Packets;

public class RemoveLogicNetworkConnection : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.RemoveLogicNetworkConnection);

        return bitStream;
    }
}
