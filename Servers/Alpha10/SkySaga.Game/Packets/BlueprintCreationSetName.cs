namespace SkySaga.Game.Packets;

public class BlueprintCreationSetName : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.BlueprintCreationSetName);

        return bitStream;
    }
}
