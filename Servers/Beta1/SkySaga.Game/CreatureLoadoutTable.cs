using System.Linq;

namespace SkySaga.Game;

public static class CreatureLoadoutTable
{
    private static readonly HashSet<uint>? AllowedSlots = Environment.GetEnvironmentVariable("SKYSAGA_CREATURE_SLOTS") is { Length: > 0 } slots
        ? [.. slots.Split(',').Select(uint.Parse)]
        : null;

    private static readonly System.Text.RegularExpressions.Regex? AllowedItems = Environment.GetEnvironmentVariable("SKYSAGA_CREATURE_ITEMS") is { Length: > 0 } pattern
        ? new(pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase)
        : null;

    private static readonly Lazy<Dictionary<string, (uint Slot, string Item, string[] Materials)[]>> ByEntity = new(Load);

    private static Dictionary<string, (uint Slot, string Item, string[] Materials)[]> Load()
    {
        var map = new Dictionary<string, (uint, string, string[])[]>(StringComparer.OrdinalIgnoreCase);
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "CreatureLoadouts.txt");

        if (!File.Exists(path))
            return map;

        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
                continue;

            map.TryAdd(parts[0], [.. parts.Skip(1).Select(ParseEquipment)]);
        }

        return map;
    }

    private static (uint, string, string[]) ParseEquipment(string token)
    {
        var slotEnd = token.IndexOf('=');
        var itemEnd = token.IndexOf(':', slotEnd);

        return (uint.Parse(token[..slotEnd]),
            token[(slotEnd + 1)..itemEnd],
            token[(itemEnd + 1)..].Split(',', StringSplitOptions.RemoveEmptyEntries));
    }

    public static List<(uint Slot, uint NameHash, uint?[] Materials, Entity? Item)> CreateItems(string entityName, IMapEntityManager entityManager)
    {
        if (!ByEntity.Value.TryGetValue(entityName, out var equipment))
            return [];

        return [.. equipment.Where(e => (AllowedSlots?.Contains(e.Slot) ?? true) && (AllowedItems?.IsMatch(e.Item) ?? true)).Select(e =>
        {
            var nameHash = Util.ComputeCrc32(e.Item);
            uint?[] materials = [.. e.Materials.Select(m => (uint?)Util.ComputeCrc32(m))];
            return (e.Slot, nameHash, materials, CreateItem(entityManager, nameHash, materials));
        })];
    }

    public static void Equip(Entity entity, List<(uint Slot, uint NameHash, uint?[] Materials, Entity? Item)> items)
    {
        if (items.Count == 0 || !entity.TryGetComponent<ClientEquippedItemComponent>(out var equipped))
            return;

        entity.TryGetComponent<ClientInventoryComponent>(out var inventory);
        var inventoryEntities = new List<int>(inventory?.InventoryEntityList ?? []);

        while (inventoryEntities.Count < ClientEquippedItemComponent.SlotCount)
            inventoryEntities.Add(0);

        foreach (var (slot, nameHash, materials, item) in items)
        {
            Guid? uuid = null;

            if (inventory is not null && item is not null)
            {
                inventoryEntities[(int)slot] = item.Id;
                uuid = item.TryGetComponent<InventoryItemComponent>(out var itemComponent)
                    && Guid.TryParse(itemComponent.InventorySlotData.ItemUUID, out var parsed) ? parsed : null;
            }

            equipped.SetSlot(slot, ItemTable.IndexOf(nameHash), uuid, materials);
        }

        if (inventory is not null)
            inventory.InventoryEntityList = inventoryEntities;
    }

    private static Entity? CreateItem(IMapEntityManager entityManager, uint nameHash, uint?[] materials)
    {
        var durable = DurabilityTable.TryGetMax(nameHash, out var durabilityMax);
        var entityType = durable ? "MaterialDurableInventoryItem" : "MaterialBasedInventoryItem";

        if (!entityManager.TryCreateEntity(entityType, out var item))
            return null;

        if (item.TryGetComponent<MaterialCompositionComponent>(out var composition))
            composition.MaterialList = materials;

        if (ItemTagTable.TryGet(nameHash, out var itemTags) && item.TryGetComponent<TagsComponent>(out var tags))
            tags.TagList = itemTags;

        if (durable && item.TryGetComponent<ClientDurabilityComponent>(out var durability))
        {
            durability.DurabilityMax = durabilityMax;
            durability.Durability = durabilityMax;
        }

        if (item.TryGetComponent<InventoryItemComponent>(out var inventoryItem))
        {
            inventoryItem.InventorySlotData.Name = nameHash;
            inventoryItem.InventorySlotData.Count = 1;
            inventoryItem.InventorySlotData.ItemUUID = Util.NewGuid();
        }

        return item;
    }
}
