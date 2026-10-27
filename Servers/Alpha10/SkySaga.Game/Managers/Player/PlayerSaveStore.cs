using System.Linq;
using Microsoft.EntityFrameworkCore;
using SkySaga.Data.Entities;
using SkySaga.Game.Database;

namespace SkySaga.Game.Managers.Player;

public static class PlayerSaveStore
{
    private static readonly Lock CacheLock = new();
    private static readonly Dictionary<Guid, PlayerSaveData?> Cached = [];
    private static readonly Dictionary<Guid, PlayerSaveData> Pending = [];

    public static PlayerSaveData? Load(Guid characterId)
    {
        lock (CacheLock)
        {
            if (Cached.TryGetValue(characterId, out var cached))
                return cached;

            var loaded = LoadFromDatabase(characterId);
            Cached[characterId] = loaded;

            return loaded;
        }
    }

    public static void Save(Guid characterId, PlayerSaveData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        lock (CacheLock)
        {
            Cached[characterId] = data;

            var queued = Pending.ContainsKey(characterId);
            Pending[characterId] = data;

            if (queued)
                return;
        }

        GameDatabase.Enqueue(context => WritePending(context, characterId));
    }

    public static bool IsNameTaken(string name, Guid characterId)
    {
        using var context = GameDatabase.Context();
        var lowered = name.ToLowerInvariant();

        return context.Characters.Any(c => c.Name.ToLower() == lowered && c.Id != characterId);
    }

    private static PlayerSaveData? LoadFromDatabase(Guid characterId)
    {
        using var context = GameDatabase.Context();

        var character = context.Characters
            .Include(c => c.Currencies)
            .Include(c => c.Inventory)
            .Include(c => c.Jobs)
            .Include(c => c.ActiveChallenges)
            .AsSplitQuery()
            .SingleOrDefault(c => c.Id == characterId);

        if (character is { LastSeenAt: null, IsCustomised: false, Inventory.Count: 0 })
        {
            Console.WriteLine($"[PlayerSaveStore] Character {character.Name} ({character.Id}) has never played: starting fresh.");
            return null;
        }

        if (character is null)
        {
            Console.WriteLine($"[PlayerSaveStore] Character {characterId} is not in the database.");
            return null;
        }

        Console.WriteLine($"[PlayerSaveStore] Loaded character {character.Name} ({character.Id}) from the database.");
        return ToSaveData(character);
    }

    private static async Task WritePending(Data.SkySagaDbContext context, Guid id)
    {
        PlayerSaveData? data;

        lock (CacheLock)
        {
            if (!Pending.Remove(id, out data))
                return;
        }

        if (!await context.Characters.AnyAsync(c => c.Id == id))
        {
            Console.WriteLine($"[PlayerSaveStore] Character {id} is not in the database, nothing saved.");
            return;
        }

        await using var transaction = await context.Database.BeginTransactionAsync();

        await context.CharacterCurrencies.Where(c => c.CharacterId == id).ExecuteDeleteAsync();
        await context.InventoryItems.Where(i => i.CharacterId == id).ExecuteDeleteAsync();
        await context.CharacterJobs.Where(j => j.CharacterId == id).ExecuteDeleteAsync();
        await context.ActiveChallenges.Where(a => a.CharacterId == id).ExecuteDeleteAsync();

        var character = await context.Characters.SingleAsync(c => c.Id == id);
        Apply(character, data);
        character.LastSeenAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private static void Apply(Character character, PlayerSaveData data)
    {
        if (!string.IsNullOrEmpty(data.Playername))
            character.Name = data.Playername;

        character.IsCustomised = data.IsCharacterCustomised;
        character.CustomisationData = data.CustomisationData;
        character.PositionMap = data.HasPosition ? data.PositionMap : null;
        character.PositionX = data.PositionX;
        character.PositionY = data.PositionY;
        character.PositionZ = data.PositionZ;
        character.YawDegrees = data.YawDegrees;
        character.LookPitch = data.LookPitch;
        character.InitialHp = data.InitialHP;
        character.WholeHearts = data.WholeHearts;
        character.HalfHearts = data.HalfHearts;
        character.Settings = data.Settings is { } settings ? ToEntity(settings) : null;
        character.ChallengeRefreshIndex = data.ChallengeRefreshIndex;
        character.AvailableEmotes = data.AvailableEmotes;
        character.KeyboundEmotes = data.KeyboundEmotes;
        character.KnownRecipes = data.KnownRecipes;
        character.CompletedChallenges = data.CompletedChallenges;

        character.Currencies = [.. data.Currencies.Select(c => new CharacterCurrency { CharacterId = character.Id, NameHash = c.NameHash, Value = c.Value })];
        character.Inventory = [.. data.Inventory.Select(i => new InventoryItem
        {
            CharacterId = character.Id,
            Slot = i.Slot,
            NameHash = i.NameHash,
            Count = i.Count,
            ItemUuid = i.ItemUuid,
            Materials = i.Materials?.Select(m => (long?)m).ToArray()
        })];
        character.Jobs = [.. data.Jobs.Select(j => new CharacterJob
        {
            CharacterId = character.Id,
            Name = j.Name,
            Rank = j.Rank,
            Experience = j.Experience,
            PrestigeRank = j.PrestigeRank,
            Available = j.Available
        })];
        character.ActiveChallenges = [.. data.ActiveChallenges.Select(a => new ActiveChallenge
        {
            CharacterId = character.Id,
            Name = a.Name,
            Progress = a.Progress,
            TodoTimestamp = a.TodoTimestamp
        })];
    }

    private static PlayerSaveData ToSaveData(Character character) => new()
    {
        Playername = character.Name,
        IsCharacterCustomised = character.IsCustomised,
        CustomisationData = character.CustomisationData,
        HasPosition = character.PositionMap is not null,
        PositionMap = character.PositionMap,
        PositionX = character.PositionX,
        PositionY = character.PositionY,
        PositionZ = character.PositionZ,
        YawDegrees = character.YawDegrees,
        LookPitch = character.LookPitch,
        InitialHP = character.InitialHp,
        WholeHearts = character.WholeHearts,
        HalfHearts = character.HalfHearts,
        Settings = character.Settings is { } settings ? ToSaveData(settings) : null,
        ChallengeRefreshIndex = (uint?)character.ChallengeRefreshIndex,
        AvailableEmotes = character.AvailableEmotes,
        KeyboundEmotes = character.KeyboundEmotes,
        KnownRecipes = character.KnownRecipes,
        CompletedChallenges = character.CompletedChallenges,
        Currencies = [.. character.Currencies.Select(c => new PlayerSaveData.CurrencyEntry { NameHash = (uint)c.NameHash, Value = c.Value })],
        Inventory = [.. character.Inventory.OrderBy(i => i.Slot).Select(i => new PlayerSaveData.InventoryEntry
        {
            Slot = i.Slot,
            NameHash = (uint)i.NameHash,
            Count = i.Count,
            ItemUuid = i.ItemUuid,
            Materials = i.Materials?.Select(m => (uint?)m).ToArray()
        })],
        Jobs = [.. character.Jobs.Select(j => new PlayerSaveData.JobEntry
        {
            Name = j.Name,
            Rank = (byte)j.Rank,
            Experience = (uint)j.Experience,
            PrestigeRank = (uint)j.PrestigeRank,
            Available = j.Available
        })],
        ActiveChallenges = [.. character.ActiveChallenges.Select(a => new PlayerSaveData.ActiveChallengeEntry
        {
            Name = a.Name,
            Progress = (uint)a.Progress,
            TodoTimestamp = (ulong)a.TodoTimestamp
        })]
    };

    private static CharacterSettings ToEntity(PlayerSaveData.SettingsEntry settings) => new()
    {
        DisableAutoQuestTracking = settings.DisableAutoQuestTracking,
        DisableSocialNotifications = settings.DisableSocialNotifications,
        DisableMysteryBoxNotifications = settings.DisableMysteryBoxNotifications,
        DisplayTimeMode = settings.DisplayTimeMode,
        QuestTrackerSort = settings.QuestTrackerSort,
        ThirdPersonCamera = settings.ThirdPersonCamera,
        CameraState = settings.CameraState,
        ControlFlag = settings.ControlFlag,
        LastActivatedSlot = settings.LastActivatedSlot,
        MiniMap = settings.MiniMap is null ? null : JsonSerializer.Serialize(settings.MiniMap),
        Hotbar = settings.Hotbar?.Select(h => new HotbarSlot { Slot = h.Slot, Hand = h.Hand, Item = h.Item, ItemUuid = h.ItemUuid }).ToList()
    };

    private static PlayerSaveData.SettingsEntry ToSaveData(CharacterSettings settings) => new()
    {
        DisableAutoQuestTracking = settings.DisableAutoQuestTracking,
        DisableSocialNotifications = settings.DisableSocialNotifications,
        DisableMysteryBoxNotifications = settings.DisableMysteryBoxNotifications,
        DisplayTimeMode = (uint)settings.DisplayTimeMode,
        QuestTrackerSort = (uint)settings.QuestTrackerSort,
        ThirdPersonCamera = settings.ThirdPersonCamera,
        CameraState = (uint)settings.CameraState,
        ControlFlag = settings.ControlFlag,
        LastActivatedSlot = (uint)settings.LastActivatedSlot,
        MiniMap = settings.MiniMap is null ? null : JsonSerializer.Deserialize<MiniMapSettings>(settings.MiniMap),
        Hotbar = settings.Hotbar?.Select(h => new PlayerSaveData.HotbarEntry { Slot = (uint)h.Slot, Hand = (uint)h.Hand, Item = h.Item, ItemUuid = h.ItemUuid }).ToList()
    };
}
