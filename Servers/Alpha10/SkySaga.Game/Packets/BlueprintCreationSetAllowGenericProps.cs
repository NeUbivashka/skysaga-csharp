namespace SkySaga.Game.Packets;

public class BlueprintCreationSetAllowGenericProps : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.BlueprintCreationSetAllowGenericProps);

        return bitStream;
    }
}
