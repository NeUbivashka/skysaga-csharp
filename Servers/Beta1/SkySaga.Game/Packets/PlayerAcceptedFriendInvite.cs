namespace SkySaga.Game.Packets;

public class PlayerAcceptedFriendInvite : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.PlayerAcceptedFriendInvite);

        return bitStream;
    }
}
