namespace SkySaga.Game.Packets;

public class ActivateCommunityQuest : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.ActivateCommunityQuest);

        return bitStream;
    }
}
