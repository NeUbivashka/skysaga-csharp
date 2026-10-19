namespace SkySaga.Game.Packets;

public static class QueueRecipeOnEntity
{
    private const uint InputListEscape = 6;

    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadFullWidthUInt32(out var entityId)) return false;
        if (!bitStream.ReadInt32(RecipeTable.VariantTableCount - 1, out var variant)) return false;
        if (!bitStream.ReadFullWidthUInt32(out var colourScheme)) return false;
        if (!bitStream.ReadByte(byte.MaxValue, out var count)) return false;
        if (!bitStream.ReadEscapedCount(InputListEscape, out var inputCount)) return false;

        List<InventoryItemRef> inputs = [];

        for (var i = 0; i < inputCount; i++)
        {
            if (!InventoryItemRef.TryDecode(bitStream, out var input)) return false;
            inputs.Add(input);
        }

        Crafting.Queue(connection, (int)entityId, (ushort)variant, colourScheme, count, inputs);
        return true;
    }
}
