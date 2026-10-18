namespace SkySaga.Game.Packets;

public class AllowHomeIslandToBeEdited : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.AllowHomeIslandToBeEdited);

        return bitStream;
    }
}
