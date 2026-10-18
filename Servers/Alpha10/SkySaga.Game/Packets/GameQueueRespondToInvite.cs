namespace SkySaga.Game.Packets;

public class GameQueueRespondToInvite : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.GameQueueRespondToInvite);

        return bitStream;
    }
}
