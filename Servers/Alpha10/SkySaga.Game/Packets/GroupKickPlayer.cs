namespace SkySaga.Game.Packets;

public class GroupKickPlayer : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.GroupKickPlayer);

        return bitStream;
    }
}
