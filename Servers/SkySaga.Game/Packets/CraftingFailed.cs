namespace SkySaga.Game.Packets;

public class CraftingFailed : ISerializablePacket
{
    public int EntityId;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.CraftingFailed);

        var bytes = BitConverter.GetBytes(EntityId);

        Array.Reverse(bytes);

        bitStream.WriteBits(bytes, 32, true);

        return bitStream;
    }
}
