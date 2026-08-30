namespace SkySaga.Game.Packets;

public class CharacterCreationResponse : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.CharacterCreationResponse);

        return bitStream;
    }
}
