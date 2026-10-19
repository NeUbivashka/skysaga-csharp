namespace SkySaga.Game.Components;

public record CraftingInput(InventoryItemRef Item, uint Quantity);

public record CraftingSlot(
    ushort Variant,
    uint ColourScheme,
    ulong StartTimeMs,
    IReadOnlyList<CraftingInput> Inputs,
    uint Quantity,
    uint ReadyQuantity)
{
    public ushort Recipe { get; init; }

    public uint?[] OutputMaterials { get; init; } = [];

    public DateTime ReadyUtc { get; init; }
}

public class ClientCraftingComponent : Component
{
    private const uint SlotListEscape = 12;
    private const uint MaxSlots = 12;
    private const uint InputListEscape = 6;
    private const uint QuantityMax = 999;

    public List<CraftingSlot> CraftingSlots { get; set => SetIfChanged(ref field, value); } = [];

    public int MaxCraftingSlots { get; set => SetIfChanged(ref field, value); } = 1;

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(CraftingSlots), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteEscapedCount((uint)CraftingSlots.Count, SlotListEscape);

            foreach (var slot in CraftingSlots)
                WriteSlot(bitStream, slot);

            return true;
        }

        if (parameterName.Equals(nameof(MaxCraftingSlots), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteRanged((uint)Math.Clamp(MaxCraftingSlots, 0, (int)MaxSlots), MaxSlots);
            return true;
        }

        return false;
    }

    private static void WriteSlot(BitStream bitStream, CraftingSlot slot)
    {
        bitStream.WriteRangedIndex(slot.Variant, RecipeTable.VariantTableCount);
        bitStream.WriteFullWidthUInt32(slot.ColourScheme);
        bitStream.WriteUInt64(slot.StartTimeMs);
        bitStream.WriteClientUuid(null);
        bitStream.WriteClientUuid(null);
        bitStream.WriteString(null);
        bitStream.Write0();
        bitStream.Write0();
        bitStream.WriteEscapedCount((uint)slot.Inputs.Count, InputListEscape);

        foreach (var input in slot.Inputs)
        {
            input.Item.Encode(bitStream);
            bitStream.WriteFullWidthUInt32(input.Quantity);
        }

        bitStream.WriteRanged(Math.Min(slot.Quantity, QuantityMax), QuantityMax);
        bitStream.WriteRanged(Math.Min(slot.ReadyQuantity, QuantityMax), QuantityMax);
    }
}
