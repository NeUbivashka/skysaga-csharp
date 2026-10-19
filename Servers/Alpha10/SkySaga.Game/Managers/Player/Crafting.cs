using System.Linq;

namespace SkySaga.Game.Managers.Player;

public static class Crafting
{
    public static void Queue(PlayerConnection connection, int entityId, ushort variant, uint colourScheme, int count, IReadOnlyList<InventoryItemRef> inputs)
    {
        var player = connection.PlayerEntity;

        Console.WriteLine($"[Crafting] queue entity {entityId} variant {variant} scheme {colourScheme} x{count}, inputs "
            + string.Join("; ", inputs.Select(input => $"{ItemTable.NameOf(input.Kind)} [{string.Join(",", input.Amounts)}] q={input.Quantity} uuid={input.ItemUuid}")));

        if (Reject(connection, entityId, variant, count, out var recipe) is { } reason)
        {
            Console.WriteLine($"[Crafting] rejected: {reason}");
            connection.Send(new CraftingFailed { EntityId = player.Id });
            return;
        }

        if (!TryMatchInputs(connection, recipe, count, inputs, out var stacks, out var reasonInputs))
        {
            Console.WriteLine($"[Crafting] rejected: {reasonInputs}");
            connection.Send(new CraftingFailed { EntityId = player.Id });
            return;
        }

        List<CraftingInput> consumed = [];

        foreach (var (input, needed, slots) in stacks)
        {
            var left = needed;

            foreach (var slot in slots)
            {
                if (left <= 0)
                    break;

                if (InventoryItems.Take(connection, player.Id, slot, left) is { } taken)
                    left -= taken.Count;
            }

            consumed.Add(new CraftingInput(new InventoryItemRef { Kind = input.Kind, Amounts = [.. input.Amounts] }, (uint)(needed - left)));
        }

        var crafting = player.TryGetComponent<ClientCraftingComponent>(out var component) ? component : null;

        if (crafting is null)
            return;

        var quantity = (uint)(recipe.OutputQuantity * count);
        var now = DateTime.UtcNow;

        List<CraftingSlot> slotsList = [.. crafting.CraftingSlots];
        slotsList.Add(new CraftingSlot(variant, colourScheme, (ulong)new DateTimeOffset(now).ToUnixTimeMilliseconds(), consumed, quantity, quantity)
        {
            Recipe = recipe.WireIndex,
            OutputMaterials = OutputMaterials(recipe, inputs),
            ReadyUtc = now.AddSeconds(recipe.Seconds * count)
        });
        crafting.CraftingSlots = slotsList;

        Console.WriteLine($"[Crafting] queued {recipe.Name} -> {quantity} x {recipe.Output} in slot {slotsList.Count - 1}, "
            + $"consumed {string.Join(", ", consumed.Select(input => $"{input.Quantity} x {ItemTable.NameOf(input.Item.Kind)}"))}");
    }

    public static void Collect(PlayerConnection connection, int entityId, int slot, bool first, bool second)
    {
        Console.WriteLine($"[Crafting] collect entity {entityId} slot {slot} ({first}, {second})");

        if (entityId != connection.PlayerEntity.Id
            || !connection.PlayerEntity.TryGetComponent<ClientCraftingComponent>(out var crafting)
            || slot < 0 || slot >= crafting.CraftingSlots.Count)
            return;

        var entry = crafting.CraftingSlots[slot];

        if (DateTime.UtcNow < entry.ReadyUtc || !RecipeTable.TryGetByWireIndex(entry.Recipe, out var recipe))
            return;

        if (!InventoryItems.Give(connection, Util.ComputeCrc32(recipe.Output), (int)entry.ReadyQuantity, entry.OutputMaterials))
        {
            Console.WriteLine($"[Crafting] {recipe.Output} not collected, inventory full");
            return;
        }

        List<CraftingSlot> slots = [.. crafting.CraftingSlots];
        slots.RemoveAt(slot);
        crafting.CraftingSlots = slots;

        Console.WriteLine($"[Crafting] collected {entry.ReadyQuantity} x {recipe.Output} [{string.Join(",", entry.OutputMaterials)}]");

        JobExperience.Award(connection, RecipeTable.ExperiencePerItem(recipe) * entry.ReadyQuantity);
    }

    private static string? Reject(PlayerConnection connection, int entityId, ushort variant, int count, out RecipeInfo recipe)
    {
        recipe = null!;
        var player = connection.PlayerEntity;

        if (entityId != player.Id)
            return $"entity {entityId} is not the player";

        if (count <= 0)
            return "count 0";

        if (!RecipeTable.TryGetByVariant(variant, out var found))
            return $"variant {variant} unknown";

        recipe = found;

        if (!player.TryGetComponent<RecipeBookComponent>(out var book) || !book.Knows(recipe.WireIndex))
            return $"{recipe.Name} not known";

        if (!player.TryGetComponent<ClientCraftingComponent>(out var crafting) || crafting.CraftingSlots.Count >= crafting.MaxCraftingSlots)
            return "no free crafting slot";

        if (recipe.Device == RecipeTable.HandCrafting)
            return null;

        var deviceId = player.TryGetComponent<ClientUseEntityComponent>(out var use) ? use.UsingEntityId : 0;

        if (!connection.WorldManager.EntityManager.TryGetEntity(deviceId, out var device)
            || !device.Name.Equals(recipe.Device, StringComparison.OrdinalIgnoreCase))
            return $"{recipe.Name} needs {recipe.Device}, using entity {deviceId}";

        return null;
    }

    private static bool TryMatchInputs(PlayerConnection connection, RecipeInfo recipe, int count, IReadOnlyList<InventoryItemRef> inputs,
        out List<(InventoryItemRef Input, int Needed, List<int> Slots)> stacks, [NotNullWhen(false)] out string? reason)
    {
        stacks = [];
        reason = null;

        var requirements = recipe.MaterialInputs.OfType<RecipeInput>().Concat(recipe.ExtraInputs).ToList();
        var used = new bool[inputs.Count];

        if (!connection.PlayerEntity.TryGetComponent<ClientInventoryComponent>(out var inventory))
        {
            reason = "no inventory";
            return false;
        }

        var entityManager = connection.WorldManager.EntityManager;
        var available = new Dictionary<int, int>();

        foreach (var requirement in requirements)
        {
            var kind = ItemTable.IndexOf(requirement.Resource);
            var index = Enumerable.Range(0, inputs.Count).FirstOrDefault(i => !used[i] && inputs[i].Kind == kind, -1);

            if (index < 0)
            {
                reason = $"no input for {requirement.Resource}";
                return false;
            }

            used[index] = true;

            var input = inputs[index];
            var needed = requirement.Quantity * count;
            List<int> slots = [];
            var total = 0;

            for (var slot = 0; slot < inventory.InventoryEntityList.Count; slot++)
            {
                if (!entityManager.TryGetEntity(inventory.InventoryEntityList[slot], out var item)
                    || !item.TryGetComponent<InventoryItemComponent>(out var component)
                    || component.InventorySlotData.Name != Util.ComputeCrc32(requirement.Resource)
                    || !ResourcePickups.MaterialsOf(item).SequenceEqual(input.Amounts))
                    continue;

                var free = available.TryGetValue(slot, out var left) ? left : component.InventorySlotData.Count;

                if (free <= 0)
                    continue;

                var isNamed = input.ItemUuid is { } uuid && Guid.TryParse(component.InventorySlotData.ItemUUID, out var itemUuid) && itemUuid == uuid;

                if (isNamed)
                    slots.Insert(0, slot);
                else
                    slots.Add(slot);

                total += free;
            }

            if (total < needed)
            {
                reason = $"{requirement.Resource}: have {total}, need {needed}";
                return false;
            }

            var remaining = needed;

            foreach (var slot in slots)
            {
                var free = available.TryGetValue(slot, out var left) ? left
                    : entityManager.TryGetEntity(inventory.InventoryEntityList[slot], out var item) && item.TryGetComponent<InventoryItemComponent>(out var component) ? component.InventorySlotData.Count : 0;
                var take = Math.Min(free, remaining);

                available[slot] = free - take;
                remaining -= take;

                if (remaining == 0)
                    break;
            }

            stacks.Add((input, needed, slots));
        }

        return true;
    }

    private static uint?[] OutputMaterials(RecipeInfo recipe, IReadOnlyList<InventoryItemRef> inputs)
    {
        List<uint?> materials = [];

        for (var slot = 0; slot < recipe.OutputMaterialSlots.Length; slot++)
        {
            if (!recipe.OutputMaterialSlots[slot])
                continue;

            materials.Add(slot < inputs.Count && inputs[slot].Amounts.Count > 0 ? inputs[slot].Amounts[0] : null);
        }

        return [.. materials];
    }
}
