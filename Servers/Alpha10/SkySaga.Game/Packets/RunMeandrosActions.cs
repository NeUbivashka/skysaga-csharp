namespace SkySaga.Game.Packets;

public class RunMeandrosActions : ISerializablePacket
{
    public uint? ActionListId;
    public int EntityId;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.RunMeandrosActions);

        InventoryItemRef.WriteOptionalUInt32(bitStream, ActionListId);
        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);

        return bitStream;
    }
}
