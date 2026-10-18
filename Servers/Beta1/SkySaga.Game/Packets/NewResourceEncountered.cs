namespace SkySaga.Game.Packets;

public class NewResourceEncountered : ISerializablePacket
{
    public InventoryItemRef Item = new();

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.NewResourceEncountered);

        Item.Encode(bitStream);

        return bitStream;
    }
}
