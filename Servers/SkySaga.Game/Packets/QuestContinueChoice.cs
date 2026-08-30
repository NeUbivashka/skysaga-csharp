namespace SkySaga.Game.Packets;

public class QuestContinueChoice : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.QuestContinueChoice);

        return bitStream;
    }
}
