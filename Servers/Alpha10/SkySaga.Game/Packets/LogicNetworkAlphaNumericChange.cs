namespace SkySaga.Game.Packets;

public class LogicNetworkAlphaNumericChange : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.LogicNetworkAlphaNumericChange);

        return bitStream;
    }
}
