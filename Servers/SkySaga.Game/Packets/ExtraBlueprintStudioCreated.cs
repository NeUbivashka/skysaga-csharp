namespace SkySaga.Game.Packets;

public class ExtraBlueprintStudioCreated : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.ExtraBlueprintStudioCreated);

        return bitStream;
    }
}
