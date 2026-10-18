namespace SkySaga.Game.Packets;

public class DebugSetSigilRank : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugSetSigilRank);

        return bitStream;
    }
}
