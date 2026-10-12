namespace SkySaga.Game.Packets;

public class StopCharge : ISerializablePacket
{
    public int EntityId;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.StopCharge);

        var bytes = BitConverter.GetBytes(EntityId);

        Array.Reverse(bytes);

        bitStream.WriteBits(bytes, 32, true);

        return bitStream;
    }
}
