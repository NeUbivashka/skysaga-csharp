namespace SkySaga.Game.Packets;

public class SetCurrentEmote : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.SetCurrentEmote);

        return bitStream;
    }
}
