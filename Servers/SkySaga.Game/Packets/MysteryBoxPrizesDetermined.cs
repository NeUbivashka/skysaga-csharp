namespace SkySaga.Game.Packets;

public class MysteryBoxPrizesDetermined : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.MysteryBoxPrizesDetermined);

        return bitStream;
    }
}
