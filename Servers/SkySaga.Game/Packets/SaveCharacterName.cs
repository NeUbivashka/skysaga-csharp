namespace SkySaga.Game.Packets;

public class SaveCharacterName : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.SaveCharacterName);

        return bitStream;
    }
}
