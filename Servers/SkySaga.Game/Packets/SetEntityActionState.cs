namespace SkySaga.Game.Packets;

public class SetEntityActionState : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.SetEntityActionState);

        return bitStream;
    }
}
