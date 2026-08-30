namespace SkySaga.Game.Packets;

public class NewResourceEncountered : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.NewResourceEncountered);

        return bitStream;
    }
}
