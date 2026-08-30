namespace SkySaga.Game.Packets;

public class SyncLogicNetwork : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.SyncLogicNetwork);

        return bitStream;
    }
}
