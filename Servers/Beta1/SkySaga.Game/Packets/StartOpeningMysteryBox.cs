namespace SkySaga.Game.Packets;

public class StartOpeningMysteryBox : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.StartOpeningMysteryBox);

        return bitStream;
    }
}
