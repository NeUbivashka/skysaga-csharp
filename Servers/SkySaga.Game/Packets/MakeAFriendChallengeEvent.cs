namespace SkySaga.Game.Packets;

public class MakeAFriendChallengeEvent : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.MakeAFriendChallengeEvent);

        return bitStream;
    }
}
