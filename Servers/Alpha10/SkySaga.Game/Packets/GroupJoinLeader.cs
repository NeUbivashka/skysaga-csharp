namespace SkySaga.Game.Packets;

public class GroupJoinLeader : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.GroupJoinLeader);

        return bitStream;
    }
}
