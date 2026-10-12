namespace SkySaga.Game.Components;

/// <summary>
/// Crafting drop slots. Client reader FUN_00D18520 (bindings vtable 0x01358A18) -> FUN_00D18620:
///
///   count     escaped at 2 (2 bits, FUN_00BCF1D0)
///   entries   count times an item reference (FUN_007E9440 = InventoryItemRef)
///
/// The client expects exactly two slots: with an empty list it indexes slot 0 of a 0x44-byte
/// vector and asserts (FTL at 0x008B2CDE, FUN_008B2CC0). The old writer had no count prefix and
/// wrote raw 32-bit ints, 97 bits the client read differently, shifting every later Player
/// parameter (sync index 28 onwards). Both slots now go out as empty item references.
/// </summary>
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
