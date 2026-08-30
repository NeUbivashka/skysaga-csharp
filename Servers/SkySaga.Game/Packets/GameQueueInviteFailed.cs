namespace SkySaga.Game.Packets;

public class GameQueueInviteFailed : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.GameQueueInviteFailed);

        return bitStream;
    }
}
