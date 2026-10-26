using System.Linq;
using Microsoft.EntityFrameworkCore;
using SkySaga.Data.Entities;
using SkySaga.Game.Database;

namespace SkySaga.Game.Managers.Player;

public static class PlayerSaveStore
{
    private static readonly string LegacySaveDirectory = Path.Combine(AppContext.BaseDirectory, "Data", "Saves");
    private static readonly bool IsSocialWorld = Environment.GetEnvironmentVariable("SKYSAGA_WORLD") == "social";

    private static readonly Lock CacheLock = new();
    private static readonly Dictionary<Guid, PlayerSaveData?> CachedPlayers = [];
    private static readonly HashSet<Guid> LoadedPlayers = [];

    public static Guid CharacterId => Guid.Parse(Util.CharacterUuid());

    public static PlayerSaveData? Load() => Load(CharacterId, Util.CharacterName(), "player.json");

    public static PlayerSaveData? Load(string fileName)
        => Load(CharacterId, Util.CharacterName(), fileName);

    public static PlayerSaveData? Load(Guid characterId, string characterName, string? legacyFileName = null)
    {
        lock (CacheLock)
        {
            if (LoadedPlayers.Contains(characterId))
                return CachedPlayers.GetValueOrDefault(characterId);

            var data = LoadFromDatabase(characterId, characterName, LegacyPath(legacyFileName));
            CachedPlayers[characterId] = data;
            LoadedPlayers.Add(characterId);

            return data;
        }
    }

    public static void Save(PlayerSaveData data, string fileName = "player.json")
        => Save(data, CharacterId, Util.CharacterName(), fileName);

    public static void Save(PlayerSaveData data, Guid characterId, string characterName, string? legacyFileName = null)
    {
        ArgumentNullException.ThrowIfNull(data);

        lock (CacheLock)
        {
            CachedPlayers[characterId] = data;
            LoadedPlayers.Add(characterId);
        }

        GameDatabase.Enqueue(context => Write(context, characterId, characterName, data, LegacyPath(legacyFileName)));
    }

    private static string? LegacyPath(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName)
            return null;

        return Path.Combine(LegacySaveDirectory, fileName);
    }

    private static PlayerSaveData? LoadFromDatabase(Guid characterId, string characterName, string? legacySavePath)
    {
        using var context = GameDatabase.Context();

        var character = context.Characters
            .Include(c => c.Currencies)
            .Include(c => c.Inventory)
            .Include(c => c.Jobs)
            .Include(c => c.ActiveChallenges)
            .AsSplitQuery()
            .SingleOrDefault(c => c.Id == characterId);

        if (character is not null)
        {
            Console.WriteLine($"[PlayerSaveStore] Loaded character {character.Name} ({character.Id}) from the database.");
            return ToSaveData(character);
        }

        var legacy = ReadLegacySave(legacySavePath);
        CreateCharacter(context, characterId, characterName, legacy);

        if (legacy is not null && legacySavePath is not null)
        {
            File.Move(legacySavePath, legacySavePath + ".imported", overwrite: true);
            Console.WriteLine($"[PlayerSaveStore] Imported {legacySavePath} into the database.");
        }

        return legacy;
    }

    private static PlayerSaveData? ReadLegacySave(string? legacySavePath)
    {
        if (legacySavePath is null || !File.Exists(legacySavePath))
            return null;

        return JsonSerializer.Deserialize<PlayerSaveData>(File.ReadAllText(legacySavePath));
    }

    private static void CreateCharacter(Data.SkySagaDbContext context, Guid characterId, string characterName, PlayerSaveData? data)
    {
        var now = DateTimeOffset.UtcNow;

        var account = context.Accounts.SingleOrDefault(a => a.Username == characterName)
            ?? context.Accounts.Add(new Account { Id = Guid.NewGuid(), Username = characterName, Role = AccountRole.Admin, CreatedAt = now }).Entity;

        var character = new Character { Id = characterId, AccountId = account.Id, Name = characterName, CreatedAt = now };

        if (data is not null)
            Apply(character, data);

        context.Characters.Add(character);
        context.SaveChanges();
    }

    private static async Task Write(Data.SkySagaDbContext context, Guid characterId, string characterName,
        PlayerSaveData data, string? legacySavePath)
    {
        var character = await context.Characters.SingleOrDefaultAsync(c => c.Id == characterId);
        if (character is null)
        {
            var legacy = ReadLegacySave(legacySavePath);
            CreateCharacter(context, characterId, characterName, legacy);
            if (legacy is not null && legacySavePath is not null && File.Exists(legacySavePath))
                File.Move(legacySavePath, legacySavePath + ".imported", overwrite: true);
            character = await context.Characters.SingleAsync(c => c.Id == characterId);
        }

        await using var transaction = await context.Database.BeginTransactionAsync();

        await context.CharacterCurrencies.Where(c => c.CharacterId == characterId).ExecuteDeleteAsync();
        await context.InventoryItems.Where(i => i.CharacterId == characterId).ExecuteDeleteAsync();
        await context.CharacterJobs.Where(j => j.CharacterId == characterId).ExecuteDeleteAsync();
        await context.ActiveChallenges.Where(a => a.CharacterId == characterId).ExecuteDeleteAsync();

        var homePosition = IsSocialWorld
            ? (character.PositionMap, character.PositionX, character.PositionY, character.PositionZ, character.YawDegrees, character.LookPitch)
            : default;
        Apply(character, data);
        if (IsSocialWorld)
        {
            character.PositionMap = homePosition.PositionMap;
            character.PositionX = homePosition.PositionX;
            character.PositionY = homePosition.PositionY;
            character.PositionZ = homePosition.PositionZ;
            character.YawDegrees = homePosition.YawDegrees;
            character.LookPitch = homePosition.LookPitch;
        }
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
