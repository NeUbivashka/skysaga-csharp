namespace SkySaga.Game.Components;

public class CraftingDropSlotsComponent : Component
{
    private const int CraftingDropSlotsDefaultCount = 2;
    public List<int> CraftingDropSlots { get; set => SetIfChanged(ref field, value); } = [];

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(CraftingDropSlots), StringComparison.OrdinalIgnoreCase))
        {

            if (CraftingDropSlots.Count >= CraftingDropSlotsDefaultCount)
            {
                bitStream.Write1();
                bitStream.Write(CraftingDropSlots.Count);
            }

            foreach (var craftingDropSlot in CraftingDropSlots)
                bitStream.Write(craftingDropSlot);

            return true;
        }

        return false;
    }
}