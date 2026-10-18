namespace SkySaga.Game.Packets;

public class DebugAddSigilProgress : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugAddSigilProgress);

        return bitStream;
    }
}
