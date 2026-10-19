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
        Device,
        Backpack,
        AdditionalHeadFeature,
    }

    public const uint SlotCount = 9;

    public ushort[] EquippedItems { get; set => SetIfChanged(ref field, value, "EquippedItemSpecs"); } = new ushort[SlotCount];

    public Guid?[] EquippedItemUuids { get; set => SetIfChanged(ref field, value, "EquippedItemSpecs"); } = new Guid?[SlotCount];

    public uint?[][] EquippedItemMaterials { get; set => SetIfChanged(ref field, value, "EquippedItemSpecs"); } = new uint?[SlotCount][];

    public bool AutoEquip { get; set => SetIfChanged(ref field, value); } = true;

    public string? LegActions { get; set => SetIfChanged(ref field, value); } = "Kick";

    public void SetSlot(uint slot, ushort itemIndex, Guid? itemUuid, uint?[]? materials = null)
    {
        if (slot >= SlotCount)
            return;

        var items = (ushort[])EquippedItems.Clone();
        var uuids = (Guid?[])EquippedItemUuids.Clone();
        var materialLists = (uint?[][])EquippedItemMaterials.Clone();

        items[slot] = itemIndex;
        uuids[slot] = itemIndex != 0 ? itemUuid : null;
        materialLists[slot] = itemIndex != 0 ? materials ?? [] : [];

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
            }

            return true;
        }

        if (parameterName.Equals(nameof(AutoEquip), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write(AutoEquip);

            return true;
        }

        if (parameterName.Equals(nameof(LegActions), StringComparison.OrdinalIgnoreCase))
        {
            if (LegActions is null)
            {
                bitStream.Write0();
                return true;
            }

            bitStream.Write1();
            bitStream.WriteFullWidthUInt32(Util.ComputeCrc32(LegActions));

            return true;
        }

        return false;
    }
}
