namespace SkySaga.Game.Packets;

public class TransferToServer : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.TransferToServer);

        return bitStream;
    }
}
