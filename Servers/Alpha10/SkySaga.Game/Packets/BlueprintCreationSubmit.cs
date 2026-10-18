namespace SkySaga.Game.Packets;

public class BlueprintCreationSubmit : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.BlueprintCreationSubmit);

        return bitStream;
    }
}
