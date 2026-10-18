namespace SkySaga.Game.Packets;

public class CraftingNotification : ISerializablePacket
{
    public uint? RecipeId;

    public int EntityId;

    public InventoryItemRef Result = new();

    public uint ResultCount;

    public InventoryItemRef Ingredient = new();

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.CraftingNotification);

        InventoryItemRef.WriteOptionalUInt32(bitStream, RecipeId);
        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);

        Result.Encode(bitStream);
        InventoryItemRef.WriteUInt32(bitStream, ResultCount);

        Ingredient.Encode(bitStream);

        return bitStream;
    }
}
