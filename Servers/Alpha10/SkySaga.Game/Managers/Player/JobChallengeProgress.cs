using System.Linq;
using System.Text.Json;

namespace SkySaga.Game.Managers.Player;

public static class JobChallengeProgress
{
    public const string Kill = "Kill";
    public const string Equip = "Equip";
    public const string Harvest = "Harvest";
    public const string Possess = "Possess";

    public static readonly IReadOnlySet<string> SupportedActions = new HashSet<string>([Kill, Equip, Harvest, Possess], StringComparer.OrdinalIgnoreCase);

    public static void Killed(PlayerConnection connection, Entity target, uint? weaponCrc, bool inOneHit)
        => Advance(connection, Kill, challenge => KillMatches(challenge.Params, target, weaponCrc, inOneHit), 1);

    public static void Equipped(PlayerConnection connection, uint itemCrc)
        => Advance(connection, Equip, challenge => ItemMatches(Property(challenge.Params, "ItemSpecFilter"), itemCrc), 1);

    public static void Harvested(PlayerConnection connection, uint itemCrc, int count)
        => Advance(connection, Harvest, challenge => ItemMatches(Property(challenge.Params, "ItemSpec"), itemCrc), (uint)Math.Max(count, 0));

    public static void InventoryChanged(PlayerConnection connection)
    {
        if (!connection.PlayerEntity.TryGetComponent<ClientJobRankComponent>(out var jobRank))
            return;

        Update(connection, jobRank, Possess, challenge => (uint)CountPossessed(connection, Property(challenge.Params, "ItemSpec")));
    }

    private static void Advance(PlayerConnection connection, string action, Func<JobChallengeInfo, bool> matches, uint amount)
    {
        if (amount == 0 || !connection.PlayerEntity.TryGetComponent<ClientJobRankComponent>(out var jobRank))
            return;

        var progress = jobRank.ActiveJobChallengeList.ToDictionary(active => active.Name, active => active.Progress, StringComparer.OrdinalIgnoreCase);
        Update(connection, jobRank, action, challenge => matches(challenge) ? progress[challenge.Name] + amount : progress[challenge.Name]);
    }

    private static void Update(PlayerConnection connection, ClientJobRankComponent jobRank, string action, Func<JobChallengeInfo, uint> next)
    {
        List<ActiveJobChallenge> active = [.. jobRank.ActiveJobChallengeList];
        List<JobChallengeInfo> reached = [];
        var changed = false;

        for (var i = 0; i < active.Count; i++)
        {
            if (!JobChallengeTable.TryGet(active[i].Name, out var challenge)
                || !challenge.Action.Equals(action, StringComparison.OrdinalIgnoreCase)
                || active[i].Progress >= challenge.Number && !IsPossess(challenge))
                continue;

            var progress = Math.Min(next(challenge), (uint)challenge.Number);

            if (progress == active[i].Progress)
                continue;

            var wasReached = active[i].Progress >= challenge.Number;

            active[i] = active[i] with { Progress = progress };
            changed = true;

            Console.WriteLine($"[Challenge] {challenge.Name} {progress}/{challenge.Number}");

            if (progress >= challenge.Number)
                reached.Add(challenge);
            else if (wasReached)
                SetTodoComplete(connection, challenge.Name, false);
        }

        if (!changed)
            return;

        jobRank.ActiveJobChallengeList = active;

        foreach (var challenge in reached)
            Reached(connection, challenge);

        connection.SaveState();
    }

    private static void Reached(PlayerConnection connection, JobChallengeInfo challenge)
    {
        if (!challenge.CompleteWhenProgressReached)
        {
            SetTodoComplete(connection, challenge.Name, true);
            Console.WriteLine($"[Challenge] {challenge.Name} ready to submit");
            return;
        }

        Finish(connection, challenge);
    }

    public static void Cancel(PlayerConnection connection, byte taskId, byte reason)
    {
        if (!connection.PlayerEntity.TryGetComponent<ClientTodoListComponent>(out var todoList)
            || todoList.TodoTaskList.Find(task => task.Id == taskId) is not { } task)
        {
            Console.WriteLine($"[Challenge] remove todo task {taskId} rejected: no such task");
            return;
        }

        if (JobChallengeTable.TryGet(task.JobChallenge, out var challenge) && !challenge.CanBeCancelled)
        {
            Console.WriteLine($"[Challenge] remove {challenge.Name} rejected: cannot be cancelled");
            return;
        }

        todoList.TodoTaskList = [.. todoList.TodoTaskList.Where(other => other.Id != taskId)];

        if (connection.PlayerEntity.TryGetComponent<ClientJobRankComponent>(out var jobRank))
            jobRank.ActiveJobChallengeList = [.. jobRank.ActiveJobChallengeList.Where(active => !active.Name.Equals(task.JobChallenge, StringComparison.OrdinalIgnoreCase))];

        connection.SaveState();

        Console.WriteLine($"[Challenge] {task.JobChallenge} cancelled (todo task {taskId}, reason {reason})");
    }

    private static void GiveLoot(PlayerConnection connection, JobChallengeInfo challenge)
    {
        if (challenge.LootTable.Length == 0)
            return;

        if (!challenge.PutAwardsInInventory
            || !connection.PlayerEntity.TryGetComponent<ClientInventoryComponent>(out var inventory))
        {
            Console.WriteLine($"[Challenge] {challenge.Name} loot {challenge.LootTable} not given: only inventory awards are supported");
            return;
        }

        var drops = LootTable.Roll(challenge.LootTable, "");
        List<int> list = [.. inventory.InventoryEntityList];

        foreach (var drop in drops)
        {
            var free = list.IndexOf(0, (int)ClientEquippedItemComponent.SlotCount);
            var name = ItemTable.NameOf(ItemTable.IndexOf(drop.ItemCrc));

            if (free < 0)
            {
                Console.WriteLine($"[Challenge] {challenge.Name} loot {drop.Count} x {name} lost: inventory full");
                continue;
            }

            var materials = drop.Materials.Select(material => material is null || material == LootTable.EntityMaterial ? (uint?)null : Util.ComputeCrc32(material)).ToArray();

            if (PlayerInitializer.CreateInventoryItem(connection.WorldManager.EntityManager, drop.ItemCrc, drop.Count, materialOverrides: materials) is not { } item)
                continue;

            list[free] = item.Id;

            connection.PlayerConnectionManager.BroadcastToAll(new EntityAdd
            {
                Id = item.Id,
                NameHash = Util.ComputeCrc32(item.Name),
                SyncData = item.GetSyncData(newEntity: true)
            });

            Console.WriteLine($"[Challenge] {challenge.Name} loot {drop.Count} x {name} in slot {free}");
        }

        if (drops.Count == 0)
            Console.WriteLine($"[Challenge] {challenge.Name} loot {challenge.LootTable} rolled nothing");

        inventory.InventoryEntityList = list;
    }

    public static void Submit(PlayerConnection connection, uint challengeCrc)
    {
        if (!JobChallengeTable.TryGet(challengeCrc, out var challenge)
            || !connection.PlayerEntity.TryGetComponent<ClientJobRankComponent>(out var jobRank)
            || !jobRank.ActiveJobChallengeList.Any(active => active.Name.Equals(challenge.Name, StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine($"[Challenge] submit 0x{challengeCrc:X8} rejected: not active");
            return;
        }

        if (IsPossess(challenge))
            InventoryChanged(connection);

        var progress = jobRank.ActiveJobChallengeList
            .Where(active => active.Name.Equals(challenge.Name, StringComparison.OrdinalIgnoreCase))
            .Select(active => active.Progress)
            .FirstOrDefault();

        if (progress < challenge.Number)
        {
            Console.WriteLine($"[Challenge] submit {challenge.Name} rejected: {progress}/{challenge.Number}");
            return;
        }

        if (IsPossess(challenge) && RequiresHandIn(challenge))
            TakeItems(connection, Property(challenge.Params, "ItemSpec"), challenge.Number);

        Finish(connection, challenge);
    }

    private static bool IsPossess(JobChallengeInfo challenge) => challenge.Action.Equals(Possess, StringComparison.OrdinalIgnoreCase);

    public static void Finish(PlayerConnection connection, JobChallengeInfo challenge)
    {
        if (!connection.PlayerEntity.TryGetComponent<ClientJobRankComponent>(out var jobRank))
            return;

        jobRank.ActiveJobChallengeList = [.. jobRank.ActiveJobChallengeList.Where(active => !active.Name.Equals(challenge.Name, StringComparison.OrdinalIgnoreCase))];

        if (!jobRank.CompletedJobChallengeList.Contains(challenge.Name, StringComparer.OrdinalIgnoreCase))
            jobRank.CompletedJobChallengeList = [.. jobRank.CompletedJobChallengeList, challenge.Name];

        if (connection.PlayerEntity.TryGetComponent<ClientTodoListComponent>(out var todoList))
            todoList.TodoTaskList = [.. todoList.TodoTaskList.Where(task => !task.JobChallenge.Equals(challenge.Name, StringComparison.OrdinalIgnoreCase))];

        Console.WriteLine($"[Challenge] {challenge.Name} complete, {challenge.Xp} XP");

        JobExperience.Award(connection, (uint)Math.Max(challenge.Xp, 0), challenge.Index);
        GiveLoot(connection, challenge);

        if (challenge.Next.Length > 0 && JobChallengeTable.TryGet(challenge.Next, out var next))
            JobChallenges.Start(connection, next);

        connection.SaveState();
    }

    private static bool RequiresHandIn(JobChallengeInfo challenge) => Flag(challenge.Params, "RequiresHandIn");

    private static void SetTodoComplete(PlayerConnection connection, string challengeName, bool complete)
    {
        if (!connection.PlayerEntity.TryGetComponent<ClientTodoListComponent>(out var todoList))
            return;

        todoList.TodoTaskList = [.. todoList.TodoTaskList.Select(task => task.JobChallenge.Equals(challengeName, StringComparison.OrdinalIgnoreCase)
            ? task with { Complete = complete }
            : task)];
    }

    private static void TakeItems(PlayerConnection connection, JsonElement filter, int amount)
    {
        if (!connection.PlayerEntity.TryGetComponent<ClientInventoryComponent>(out var inventory))
            return;

        var entityManager = connection.WorldManager.EntityManager;
        List<int> list = [.. inventory.InventoryEntityList];

        for (var slot = 0; slot < list.Count && amount > 0; slot++)
        {
            if (list[slot] == 0
                || !entityManager.TryGetEntity(list[slot], out var item)
                || !item.TryGetComponent<InventoryItemComponent>(out var itemComponent)
                || itemComponent.InventorySlotData.Name is not uint crc
                || !ItemMatches(filter, crc))
                continue;

            var taken = Math.Min(amount, itemComponent.InventorySlotData.Count);
            amount -= taken;

            Console.WriteLine($"[Challenge] handed in {taken} x {ItemTable.NameOf(ItemTable.IndexOf(crc))} from slot {slot}");

            if (itemComponent.InventorySlotData.Count > taken)
            {
                itemComponent.InventorySlotData = itemComponent.InventorySlotData.WithCount(itemComponent.InventorySlotData.Count - taken);
                continue;
            }

            list[slot] = 0;
            entityManager.RemoveEntity(item.Id);
            connection.PlayerConnectionManager.BroadcastRemovedAfterSync(item.Id);
        }

        inventory.InventoryEntityList = list;
    }

    private static bool KillMatches(JsonElement parameters, Entity target, uint? weaponCrc, bool inOneHit)
    {
        var check = Property(parameters, "KillCheckParams");
        var filter = Property(check, "EntityFilter");

        if (Text(filter, "SelectionType") is { Length: > 0 } selection && selection != "EntityWithTag")
            return false;

        if (Text(filter, "EntityWithTag") is { Length: > 0 } tag
            && !target.TextListParameter("taglist").Contains(tag, StringComparer.OrdinalIgnoreCase))
            return false;

        if (Flag(check, "InOneHit") && !inOneHit)
            return false;

        return ItemMatches(Property(check, "ItemSpecFilter"), weaponCrc);
    }

    private static bool ItemMatches(JsonElement filter, uint? itemCrc)
    {
        if (!Flag(filter, "TestForItem"))
            return true;

        if (itemCrc is not uint crc)
            return false;

        if (Flag(filter, "SpecificItem"))
        {
            var resource = Text(Property(Property(filter, "ItemSpec"), "ItemSpec"), "Resource");
            return !string.IsNullOrEmpty(resource) && Util.ComputeCrc32(resource) == crc;
        }

        var entries = Property(Property(filter, "ResourceTags"), "FilterEntries");

        if (entries.ValueKind != JsonValueKind.Array)
            return true;

        var itemTags = ItemTagTable.TryGet(crc, out var tags) ? tags : [];

        foreach (var entry in entries.EnumerateArray())
        {
            var values = Property(Property(entry, "TagList"), "Values");
            List<uint> wanted = values.ValueKind == JsonValueKind.Array
                ? [.. values.EnumerateArray().Select(value => Util.ComputeCrc32(Text(value, "Value") ?? ""))]
                : [];

            var matches = Text(entry, "Logic") == "Or"
                ? wanted.Any(itemTags.Contains)
                : wanted.All(itemTags.Contains);

            if (matches == Flag(entry, "InverseLogic"))
                return false;
        }

        return true;
    }

    private static int CountPossessed(PlayerConnection connection, JsonElement filter)
    {
        if (!connection.PlayerEntity.TryGetComponent<ClientInventoryComponent>(out var inventory))
            return 0;

        var entityManager = connection.WorldManager.EntityManager;
        var count = 0;

        foreach (var entityId in inventory.InventoryEntityList)
        {
            if (entityId != 0
                && entityManager.TryGetEntity(entityId, out var item)
                && item.TryGetComponent<InventoryItemComponent>(out var itemComponent)
                && itemComponent.InventorySlotData.Name is uint crc
                && ItemMatches(filter, crc))
                count += itemComponent.InventorySlotData.Count;
        }

        return count;
    }

    private static JsonElement Property(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : default;

    private static string? Text(JsonElement element, string name)
        => Property(element, name) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;

    private static bool Flag(JsonElement element, string name)
        => Property(element, name).ValueKind == JsonValueKind.True;
}
