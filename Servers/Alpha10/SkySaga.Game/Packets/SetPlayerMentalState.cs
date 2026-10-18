namespace SkySaga.Game.Packets;

public class SetPlayerMentalState : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.SetPlayerMentalState);

        return bitStream;
    }
}
