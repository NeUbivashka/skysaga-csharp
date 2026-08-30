namespace SkySaga.Game.Packets;

public class SetCharacterCustomisationData : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.SetCharacterCustomisationData);

        return bitStream;
    }
}
