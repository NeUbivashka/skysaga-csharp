namespace SkySaga.Game.Packets;

public class AddLogicNetworkConnection : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.AddLogicNetworkConnection);

        return bitStream;
    }
}
