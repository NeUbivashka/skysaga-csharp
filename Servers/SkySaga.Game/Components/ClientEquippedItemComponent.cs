namespace SkySaga.Game.Components;

/// <summary>
/// What the player wears and holds. Client bindings vtable 0x01341B1C, read function FUN_00BFF590:
///
///   binding  parameter          wire
///   -------  -----------------  ----------------------------------------------------------------
///      0     equippeditemspecs  count escaped at 9 (4 bits, FUN_00BF99A0), then one item
///                               reference per slot (FUN_007E9440 = InventoryItemRef)
///      1     autoequip          1 bit
///      2     legactions         presence bit + full-width u32 (FUN_007EA4B0); absent = default
///                               ("kick" in Entities.json)
///
/// The nine entries are the equipment slots in the client's order (names at 0x013415D4):
/// LeftHand, RightHand, Head, Torso, Legs, Arms, Device, Backpack, AdditionalHeadFeature.
/// An empty slot is a reference with item index 0.
/// </summary>
public class ClientEquippedItemComponent : Component
{
    public enum Slot
    {
        LeftHand,
        RightHand,
        Head,
        Torso,
        Legs,
        Arms,
        Device,
        Backpack,
        AdditionalHeadFeature,
    }

    public const uint SlotCount = 9;

    /// <summary>Item table index per slot (see ItemTable); 0 = empty.</summary>
    public ushort[] EquippedItems { get; set => SetIfChanged(ref field, value); } = new ushort[SlotCount];

    /// <summary>The inventory item's UUID per slot, linking the equipped item to its inventory entity.</summary>
    public Guid?[] EquippedItemUuids { get; set => SetIfChanged(ref field, value); } = new Guid?[SlotCount];

    public bool AutoEquip { get; set => SetIfChanged(ref field, value); } = true;

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals("EquippedItemSpecs", StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteEscapedCount(SlotCount, SlotCount);

            for (var i = 0; i < SlotCount; i++)
            {
                new InventoryItemRef
                {
                    Kind = EquippedItems[i],
                    ItemUuid = EquippedItems[i] != 0 ? EquippedItemUuids[i] : null,
                }.Encode(bitStream);
            }

            return true;
        }

        if (parameterName.Equals(nameof(AutoEquip), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write(AutoEquip);

            return true;
        }

        if (parameterName.Equals("LegActions", StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write0();

            return true;
        }

        return false;
    }
}
