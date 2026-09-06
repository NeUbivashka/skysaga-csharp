namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 83, server to client. Reader: FUN_007EF830 -> FUN_00BB3E50(1, 0x53, ...).
///
///     RecipeId    1 presence bit + 32 bits
///     EntityId    32 bits, full width -> big-endian
///     Result      FUN_00C04130: one InventoryItemRef followed by 32 bits
///     Ingredient  one InventoryItemRef (FUN_007E9440)
///
/// Two item references in one packet, which is what a crafting notification would carry - what
/// came out and what went in. Which is which is a reading of the order, not of the code.
/// </summary>
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
