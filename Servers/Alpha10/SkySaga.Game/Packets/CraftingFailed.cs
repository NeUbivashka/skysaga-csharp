namespace SkySaga.Game.Packets;

public class CraftingFailed : ISerializablePacket
{
    public int EntityId;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.CraftingFailed);
        bitStream.WriteFullWidthUInt32((uint)EntityId);

        return bitStream;
    }
}
