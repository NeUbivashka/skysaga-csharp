namespace SkySaga.Game.Packets;

public class SetClientEntity : ISerializablePacket
{
    public int EntityId;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.SetClientLocalPlayerEntity);

        bitStream.Write(EntityId);

        return bitStream;
    }
}