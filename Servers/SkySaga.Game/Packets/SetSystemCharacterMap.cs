namespace SkySaga.Game.Packets;

public class SetSystemCharacterMap : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.SetSystemCharacterMap);

        return bitStream;
    }
}
