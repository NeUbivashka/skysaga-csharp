namespace SkySaga.Game.Packets;

public class SetPlayerSigil : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.SetPlayerSigil);

        return bitStream;
    }
}
