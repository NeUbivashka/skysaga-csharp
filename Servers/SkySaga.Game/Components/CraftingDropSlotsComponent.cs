namespace SkySaga.Game.Components;

public class CraftingDropSlotsComponent : Component
{
    private const uint CountEscape = 2;
    private const int SlotCount = 2;

    public List<int> CraftingDropSlots { get; set => SetIfChanged(ref field, value); } = [];

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(CraftingDropSlots), StringComparison.OrdinalIgnoreCase))
        {
            if (Environment.GetEnvironmentVariable("SKYSAGA_EMPTY_DROP_SLOTS") == "1")
            {
                bitStream.WriteEscapedCount(0, CountEscape);

                return true;
            }

            bitStream.WriteEscapedCount(SlotCount, CountEscape);

            for (var i = 0; i < SlotCount; i++)
                new InventoryItemRef().Encode(bitStream);

            return true;
        }

        return false;
    }
}
