namespace SkySaga.Game.Packets;

public class DebugRequestLootBalancerInfo : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugRequestLootBalancerInfo);

        return bitStream;
    }
}
