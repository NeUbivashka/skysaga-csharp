namespace SkySaga.Game.Packets;

public class BlueprintRevokeResult : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.BlueprintRevokeResult);

        return bitStream;
    }
}
