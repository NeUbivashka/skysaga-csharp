namespace SkySaga.Game.Packets;

public class DebugReturnLootBalancerInfo : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugReturnLootBalancerInfo);

        return bitStream;
    }
}
