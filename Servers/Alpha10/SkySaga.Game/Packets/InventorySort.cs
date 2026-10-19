using System.Linq;

namespace SkySaga.Game.Packets;

public static class InventorySort
{
    private const byte ModeMax = 4;
    private const byte ByType = 0;
    private const byte ByRarity = 1;

    private readonly record struct Slot(int EntityId, ItemSortInfo? Info);

    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadFullWidthUInt32(out var entityId) || !bitStream.ReadByte(ModeMax, out var mode) || bitStream.GetNumberOfUnreadBits() < 1)
            return false;

        var reverse = bitStream.ReadBit();

        if (entityId != connection.PlayerEntity.Id
            || !connection.PlayerEntity.TryGetComponent<ClientInventoryComponent>(out var inventory)
            || mode is not (ByType or ByRarity))
        {
            Console.WriteLine($"[Inventory] sort of entity {entityId} mode {mode} not supported");
            return true;
        }

        var entityManager = connection.WorldManager.EntityManager;
        var first = (int)ClientEquippedItemComponent.SlotCount;
        var list = inventory.InventoryEntityList;

        var slots = list.Skip(first)
            .Select(id => new Slot(id, id != 0
                && entityManager.TryGetEntity(id, out var item)
                && item.TryGetComponent<InventoryItemComponent>(out var component)
                && component.InventorySlotData.Name is uint crc
                && ItemSortTable.TryGet(crc, out var info) ? info : null))
            .ToList();

        var sorted = slots.OrderBy(slot => slot, Comparer<Slot>.Create((a, b) => Less(a, b, mode, reverse) ? -1 : Less(b, a, mode, reverse) ? 1 : 0)).ToList();

        inventory.InventoryEntityList = [.. list.Take(first), .. sorted.Select(slot => slot.EntityId)];

        Console.WriteLine($"[Inventory] sorted {sorted.Count} backpack slots, mode {mode}, reverse {reverse}");
        return true;
    }

    private static bool Less(Slot a, Slot b, byte mode, bool reverse)
    {
        if (a.EntityId == 0 != (b.EntityId == 0))
            return a.EntityId != 0;

        if (reverse)
            (a, b) = (b, a);

        if (mode == ByRarity)
        {
            var rarityA = a.Info?.Rarity ?? 0;
            var rarityB = b.Info?.Rarity ?? 0;

            if (rarityA != rarityB)
                return rarityA > rarityB;
        }

        var typeA = a.Info?.ObjectType ?? ItemSortTable.NoObjectType;
        var typeB = b.Info?.ObjectType ?? ItemSortTable.NoObjectType;

        if (typeA == ItemSortTable.NoObjectType)
            return false;

        if (typeA == typeB)
            return string.CompareOrdinal(a.Info?.Name, b.Info?.Name) < 0;

        return typeA < typeB;
    }
}
