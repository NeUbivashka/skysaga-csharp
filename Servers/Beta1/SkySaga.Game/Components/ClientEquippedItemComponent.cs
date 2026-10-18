namespace SkySaga.Game.Components;

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
        Backpack,
        Special,
        AdditionalHeadFeature,
        InfiniteLeftHand,
        InfiniteRightHand,
    }

    public const uint SlotCount = 11;

    private const uint PerModeEscape = 5;
    private const ushort NoInventorySlot = 0xFFFF;

    public ushort[] EquippedItems { get; set => SetIfChanged(ref field, value, "EquippedItemSpecs"); } = new ushort[SlotCount];

    public Guid?[] EquippedItemUuids { get; set => SetIfChanged(ref field, value, "EquippedItemSpecs"); } = new Guid?[SlotCount];

    public uint?[][] EquippedItemMaterials { get; set => SetIfChanged(ref field, value, "EquippedItemSpecs"); } = new uint?[SlotCount][];

    public ushort[] InventorySlots { get; set => SetIfChanged(ref field, value, "EquippedItemSpecs"); } = new ushort[SlotCount];

    public bool AutoEquip { get; set => SetIfChanged(ref field, value); } = true;

    public void SetSlot(uint slot, ushort itemIndex, Guid? itemUuid, uint?[]? materials = null, int inventorySlot = 0)
    {
        if (slot >= SlotCount)
            return;

        var items = (ushort[])EquippedItems.Clone();
        var uuids = (Guid?[])EquippedItemUuids.Clone();
        var materialLists = (uint?[][])EquippedItemMaterials.Clone();

        items[slot] = itemIndex;
        uuids[slot] = itemIndex != 0 ? itemUuid : null;
        materialLists[slot] = itemIndex != 0 ? materials ?? [] : [];

        var inventorySlots = (ushort[])InventorySlots.Clone();
        inventorySlots[slot] = itemIndex != 0 ? (ushort)inventorySlot : (ushort)0;

        InventorySlots = inventorySlots;
        EquippedItemMaterials = materialLists;
        EquippedItemUuids = uuids;
        EquippedItems = items;
    }

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
                    Amounts = EquippedItems[i] != 0 && EquippedItemMaterials[i] is { } materials ? [.. materials] : [],
                    ItemUuid = EquippedItems[i] != 0 ? EquippedItemUuids[i] : null,
                }.Encode(bitStream);

                bitStream.WriteFullWidthUInt16(EquippedItems[i] != 0 ? InventorySlots[i] : NoInventorySlot);
            }

            return true;
        }

        if (parameterName.Equals("LeftHandPerModeItemSpec", StringComparison.OrdinalIgnoreCase)
            || parameterName.Equals("RightHandPerModeItemSpec", StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteEscapedCount(0, PerModeEscape);

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
