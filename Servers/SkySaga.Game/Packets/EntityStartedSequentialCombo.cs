namespace SkySaga.Game.Packets;

public class EntityStartedSequentialCombo : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.EntityStartedSequentialCombo);

        return bitStream;
    }
}
