using System.Linq;
namespace SkySaga.Game.Managers.Player;

public class PlayerInitializer(IWorldManager worldManager)
{
    private const byte BackpackSlots = 42;

    private const uint ForestMasterSigil = 10;
    private const byte VaultSlots = 200;
    private const string DefaultPlayerCustomisation = "hub_npc_explorer_customisation";
    private static readonly uint[] StarterSigils = [2, 8, ForestMasterSigil, 14];

    private readonly IWorldManager _worldManager = worldManager ?? throw new ArgumentNullException(nameof(worldManager));
    private IMapEntityManager _entityManager => _worldManager.EntityManager;

    public void InitializePlayer(Entity player, PlayerConnection connection)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(connection);

        var save = connection.SaveFileName is { } saveFileName
            ? PlayerSaveStore.Load(connection.Identity.CharacterUuid, connection.Identity.Name, saveFileName)
            : null;
        if (save?.CompletedNarrativeIndices is { } completedNarratives)
            connection.CompletedNarrativeIndices.UnionWith(completedNarratives);

        if (player.TryGetComponent<ClientPlayernameComponent>(out var clientPlayernameComponent))
        {
            clientPlayernameComponent.Playername = !string.IsNullOrEmpty(save?.Playername)
                ? save.Playername
                : connection.Identity.Name;

            var sigilFields = (Environment.GetEnvironmentVariable("SKYSAGA_SIGIL_FIELDS") ?? "0,0").Split(',');
            var sigilA = uint.TryParse(sigilFields[0], out var a) ? a : 0u;
            var sigilB = sigilFields.Length > 1 && uint.TryParse(sigilFields[1], out var b) ? b : 0u;

            clientPlayernameComponent.AvailablePlayerSigilList = [.. StarterSigils.Select(index => (index, sigilA, sigilB))];
            clientPlayernameComponent.CurrentSigil = ForestMasterSigil;
        }

        if (player.TryGetComponent<ClientOwnerComponent>(out var owner))
            owner.Owner = connection.Identity.CharacterUuid;

        if (player.TryGetComponent<UserComponent>(out var user))
            user.CharacterUuid = connection.Identity.CharacterUuid;

        if (player.TryGetComponent<ClientShopComponent>(out var shop))
            shop.ShopList = [new ClientShopComponent.Shop { Items = GlobalShopItems() }, new ClientShopComponent.Shop()];

        if (player.TryGetComponent<TagsComponent>(out var tags))
            tags.TagList = [Util.ComputeCrc32("Player")];

        if (player.TryGetComponent<ClientCharacterCustomisationComponent>(out var customisation))
        {
            if (!string.IsNullOrEmpty(save?.CustomisationData))
            {
                customisation.IsCharacterCustomised = save.IsCharacterCustomised;
                customisation.CustomisationData = Convert.FromBase64String(save.CustomisationData);
            }
            else
            {
                customisation.CustomisationListCrc = DefaultPlayerCustomisation;
                Console.WriteLine($"[PlayerInitializer] No saved appearance for {connection.Identity.CharacterUuid}; using {DefaultPlayerCustomisation}.");
            }
        }

        if (player.TryGetComponent<ClientSettingsComponent>(out var settings) && save?.Settings is { } saved)
        {
            settings.DisableAutoQuestTracking = saved.DisableAutoQuestTracking;
            settings.DisableSocialNotifications = saved.DisableSocialNotifications;
            settings.DisableMysteryBoxNotifications = saved.DisableMysteryBoxNotifications;
            settings.DisplayTimeMode = saved.DisplayTimeMode;
            settings.QuestTrackerSort = saved.QuestTrackerSort;
            settings.ThirdPersonCamera = saved.ThirdPersonCamera;
            settings.CameraState = saved.CameraState;
            settings.ControlFlag = saved.ControlFlag;
            settings.LastActivatedSlot = saved.LastActivatedSlot;

            if (saved.MiniMap is { } miniMap)
                settings.MiniMap = miniMap;

            foreach (var entry in saved.Hotbar ?? [])
                settings.SetHotbarItem(entry.Slot, entry.Hand,
                    new InventoryItemRef { Kind = ItemTable.IndexOf(entry.Item), ItemUuid = entry.ItemUuid });
        }

        if (player.TryGetComponent<ClientHealthComponent>(out var clientHealthComponent))
        {
            clientHealthComponent.InitialHP = save is { InitialHP: > 0 } ? save.InitialHP : 10;
            clientHealthComponent.WholeHearts = save is { WholeHearts: > 0 } ? save.WholeHearts : 20;

            if (save is not null)
                clientHealthComponent.HalfHearts = save.HalfHearts;
        }

        if (player.TryGetComponent<SmoothedTransformComponent>(out var smoothedTransformComponent))
        {
            var isSocialHub = Environment.GetEnvironmentVariable("SKYSAGA_WORLD") == "social";
            // The City arrival sequence is anchored to its airship landing pad.
            // A previous City position can be many voxels away from that prop,
            // leaving the player and camera out of sync when the sequence starts.
            // Keep appearance, inventory, settings and progress, but always use
            // the current landing point for a social-hub entry.
            var savedPosition = !isSocialHub && save is { HasPosition: true } && save.PositionMap == WorldMapLoader.MapFileName
                ? new Vector3(save.PositionX, save.PositionY, save.PositionZ)
                : (Vector3?)null;

            if (isSocialHub && save is { HasPosition: true })
                Console.WriteLine($"[PlayerInitializer] Ignoring cached City position "
                    + $"({save.PositionX}, {save.PositionY}, {save.PositionZ}); using the airship landing point.");

            if (savedPosition is { } position && SpawnFinder.IsSolidAt(_worldManager.ChunkManager, position))
            {
                var lifted = position with { Y = MathF.Floor(position.Y) + 1 };

                if (SpawnFinder.IsSolidAt(_worldManager.ChunkManager, lifted))
                {
                    Console.WriteLine($"[PlayerInitializer] Saved position {position} is inside a solid voxel - ignoring it.");
                    savedPosition = null;
                }
                else
                {
                    savedPosition = lifted;
                }
            }

            if (savedPosition is { } validPosition)
            {
                smoothedTransformComponent.Position = validPosition;
                smoothedTransformComponent.YawDegrees = save!.YawDegrees;
            }
            else
            {
                (smoothedTransformComponent.Position, smoothedTransformComponent.YawDegrees) = _worldManager.GetPlayerLandingPoint();
            }

            if (float.TryParse(Environment.GetEnvironmentVariable("SKYSAGA_SPAWN_Y"),
                    System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var spawnY))
            {
                var p = smoothedTransformComponent.Position;
                smoothedTransformComponent.Position = new Vector3(p.X, spawnY, p.Z);
                Console.WriteLine($"[PlayerInitializer] SKYSAGA_SPAWN_Y={spawnY} - spawn moved to {smoothedTransformComponent.Position}.");
            }
        }

        if (player.TryGetComponent<ClientFeatureUnlockComponent>(out var clientFeatureUnlockComponent))
            clientFeatureUnlockComponent.UnlockedFeatures =
                [.. FeatureUnlockComponent.AllFeatures.Except(FeatureUnlockComponent.KeptLocked)];

        if (player.TryGetComponent<ClientCraftingDropSlotsComponent>(out var clientCraftingDropSlotsComponent))
            clientCraftingDropSlotsComponent.CraftingDropSlots = [0, 0];

        if (player.TryGetComponent<ClientPlayerAspectsComponent>(out var clientPlayerAspectsComponent))
        {
            clientPlayerAspectsComponent.CanEditMap = true;
            clientPlayerAspectsComponent.CanDamageEntities = true;
            clientPlayerAspectsComponent.CanDamagePlayers = true;
            clientPlayerAspectsComponent.CanCreateDevices = true;
            clientPlayerAspectsComponent.CanDamageDevices = true;
            clientPlayerAspectsComponent.IsDebugPlayer = true;
            clientPlayerAspectsComponent.AccountLevel = 2;
            clientPlayerAspectsComponent.Permissions = [.. PlayerAspectsComponent.DefaultPermissions];
        }

        if (player.TryGetComponent<ClientEmoteComponent>(out var emotes))
        {
            emotes.AvailableEmotes = [.. (save?.AvailableEmotes ?? [.. EmoteTable.ForNewPlayers])
                .Select(EmoteTable.IndexOf)
                .Where(index => index != 0)
                .Distinct()];
            emotes.KeyboundEmotes = [.. (save?.KeyboundEmotes ?? [.. player.TextListParameter("keyboundemotes")])
                .Select(name => name.Length == 0 ? (ushort)0 : EmoteTable.IndexOf(name))
                .Concat(Enumerable.Repeat((ushort)0, ClientEmoteComponent.KeyboundSlots))
                .Take(ClientEmoteComponent.KeyboundSlots)];
        }

        if (player.TryGetComponent<RecipeBookComponent>(out var recipeBook))
            recipeBook.RecipeList = [.. (save?.KnownRecipes ?? [.. RecipeTable.All.Where(recipe => recipe.NewPlayers).Select(recipe => recipe.Name)])
                .Select(name => RecipeTable.TryGetByName(name, out var recipe) ? recipe : null)
                .OfType<RecipeInfo>()
                .DistinctBy(recipe => recipe.WireIndex)
                .Select(recipe => new KnownRecipe(recipe.WireIndex, recipe.Variants))];

        if (player.TryGetComponent<ClientJobRankComponent>(out var clientJobRankComponent))
        {
            var refreshIndex = JobChallenges.RefreshIndex(DateTime.UtcNow);
            var completed = save?.CompletedChallenges ?? JobChallenges.TutorialCompleted();

            if (save?.ChallengeRefreshIndex is { } savedIndex && savedIndex != refreshIndex)
                completed = JobChallenges.ResetDailies(completed);
            List<ActiveJobChallenge> active = [.. (save?.ActiveChallenges ?? [])
                .Where(entry => JobChallengeTable.TryGet(entry.Name, out _))
                .Select(entry => new ActiveJobChallenge(entry.Name, entry.Progress))];

            clientJobRankComponent.CompletedJobChallengeList = completed;
            clientJobRankComponent.ActiveJobChallengeList = active;
            var lastRefreshIndex = save?.ChallengeRefreshIndex ?? refreshIndex;

            clientJobRankComponent.NormalJobChallengeRefreshData = JobChallenges.RefreshData(lastRefreshIndex);
            clientJobRankComponent.MasterJobChallengeRefreshData = JobChallenges.RefreshData(lastRefreshIndex);

            var ranks = (save?.Jobs ?? [])
                .GroupBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Rank, StringComparer.OrdinalIgnoreCase);

            clientJobRankComponent.JobList = [.. JobTable.All
                .Where(job => !job.Hidden)
                .Select(job =>
                {
                    var saved = save?.Jobs.FirstOrDefault(entry => entry.Name.Equals(job.Name, StringComparison.OrdinalIgnoreCase));

                    return new JobProgress(job.Name, saved?.Rank ?? 0, saved?.Experience ?? 0, saved?.PrestigeRank ?? 0)
                    {
                        NormalAvailable = saved?.Available is { } offers && save?.ChallengeRefreshIndex == refreshIndex
                            ? JobChallenges.KeepOffered(job, offers, ranks, completed, active)
                            : JobChallenges.RollDailies(job, ranks, completed, active)
                    };
                })];
            clientJobRankComponent.QuestsForEntityList = JobChallenges.OffersFor(_entityManager, clientJobRankComponent.JobList);

            if (player.TryGetComponent<ClientTodoListComponent>(out var todoList))
                todoList.TodoTaskList = JobChallenges.TodoTasksFor(active, (save?.ActiveChallenges ?? [])
                    .GroupBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.First().TodoTimestamp, StringComparer.OrdinalIgnoreCase));
        }

        if (player.TryGetComponent<ClientWalletComponent>(out var clientWalletComponent))
        {
            if (save is { Currencies.Count: > 0 })
            {
                foreach (var currency in save.Currencies)
                {
                    clientWalletComponent.WalletCurrency.CurrencyList.Add(new WalletData.CurrencyData
                    {
                        NameHash = currency.NameHash,
                        Value = currency.Value
                    });
                }
            }
            else
            {
                clientWalletComponent.WalletCurrency.CurrencyList.Add(new WalletData.CurrencyData
                {
                    NameHash = Util.ComputeCrc32("Life_Ticket"),
                    Value = 69
                });

                clientWalletComponent.WalletCurrency.CurrencyList.Add(new WalletData.CurrencyData
                {
                    NameHash = Util.ComputeCrc32("Portal_Ticket"),
                    Value = 420
                });
            }

            foreach (var (currency, amount) in StartingCurrencies)
            {
                if (!clientWalletComponent.WalletCurrency.CurrencyList.Any(entry => entry.NameHash == Util.ComputeCrc32(currency)))
                    clientWalletComponent.WalletCurrency.CurrencyList.Add(new WalletData.CurrencyData { NameHash = Util.ComputeCrc32(currency), Value = amount });
            }
        }

        if (player.TryGetComponent<ClientInventoryComponent>(out var clientInventoryComponent))
        {
            clientInventoryComponent.MaxInventorySlots = (byte)(BackpackSlots + ClientEquippedItemComponent.SlotCount);

            for (int i = 0; i < clientInventoryComponent.MaxInventorySlots; i++)
                clientInventoryComponent.InventoryEntityList.Add(0);

            if (Environment.GetEnvironmentVariable("SKYSAGA_NO_INVENTORY") == "1")
            {
            }
            else if (save is { Inventory.Count: > 0 })
            {
                foreach (var entry in save.Inventory)
                {
                    if (entry.Slot < 0 || entry.Slot >= clientInventoryComponent.MaxInventorySlots)
                        continue;

                    CreateAndEquipInventoryItem(clientInventoryComponent, entry.Slot, entry.NameHash, entry.Count, entry.ItemUuid, entry.Materials);
                }

                EnsureItem(clientInventoryComponent, save.Inventory, "Tool_Explorer_Generic_Pickaxe_1");
                EnsureItem(clientInventoryComponent, save.Inventory, "Camera");
            }
            else
            {
                CreateAndEquipInventoryItem(clientInventoryComponent, (int)ClientEquippedItemComponent.Slot.Head, "Armour_Explorer_Generic_Medium_1_Head");
                CreateAndEquipInventoryItem(clientInventoryComponent, (int)ClientEquippedItemComponent.Slot.Torso, "Armour_Explorer_Generic_Medium_1_Torso");
                CreateAndEquipInventoryItem(clientInventoryComponent, (int)ClientEquippedItemComponent.Slot.Legs, "Armour_Explorer_Generic_Medium_1_Legs");
                CreateAndEquipInventoryItem(clientInventoryComponent, (int)ClientEquippedItemComponent.Slot.Arms, "Armour_Explorer_Generic_Medium_1_Arms");

                CreateAndEquipInventoryItem(clientInventoryComponent, 9, "Dirt", 10);
                CreateAndEquipInventoryItem(clientInventoryComponent, 10, "Tool_Explorer_Generic_Torch", 10);
                CreateAndEquipInventoryItem(clientInventoryComponent, 11, "Anvil_2x1x1", 1);
                CreateAndEquipInventoryItem(clientInventoryComponent, 13, "Camp_Fire", 1);
                CreateAndEquipInventoryItem(clientInventoryComponent, 14, "Weapon_Forest_Royalist_Dagger_0", 1);
                CreateAndEquipInventoryItem(clientInventoryComponent, 15, "Weapon_Forest_Royalist_Sword_0", 1);
                CreateAndEquipInventoryItem(clientInventoryComponent, 16, "Tool_Explorer_Generic_Pickaxe_0", 1);
                CreateAndEquipInventoryItem(clientInventoryComponent, 12, "Tool_Explorer_Generic_Pickaxe_1", 1);

                CreateAndEquipInventoryItem(clientInventoryComponent, 17, "Stone", 10);
                CreateAndEquipInventoryItem(clientInventoryComponent, 18, "Clay", 10);

                CreateAndEquipInventoryItem(clientInventoryComponent, 19, "Flint_and_Tinder", 1);
                CreateAndEquipInventoryItem(clientInventoryComponent, 20, "Camera", 1);
            }

            CreateVault(clientInventoryComponent);

            if (player.TryGetComponent<ClientEquippedItemComponent>(out var equipped))
                EquipArmourFromInventory(clientInventoryComponent, equipped);

            if (player.TryGetComponent<ClientSettingsComponent>(out var hotbarSettings))
            {
                if (save?.Settings?.Hotbar is null)
                    FillDefaultHotbar(clientInventoryComponent, hotbarSettings);
                else
                    AttachHotbarMaterials(clientInventoryComponent, hotbarSettings);
            }

            connection.SyncEquippedHotbar();
        }

        if (save is null && connection.SaveFileName is { } newSaveFileName)
            PlayerSaveStore.Save(CaptureSaveData(player), connection.Identity.CharacterUuid,
                connection.Identity.Name, newSaveFileName);
    }

    public PlayerSaveData CaptureSaveData(Entity player)
    {
        ArgumentNullException.ThrowIfNull(player);

        var data = new PlayerSaveData();

        if (player.TryGetComponent<ClientPlayernameComponent>(out var clientPlayernameComponent))
            data.Playername = clientPlayernameComponent.Playername;

        if (player.TryGetComponent<ClientCharacterCustomisationComponent>(out var customisation))
        {
            data.IsCharacterCustomised = customisation.IsCharacterCustomised;
            data.CustomisationData = customisation.CustomisationData is { } bytes ? Convert.ToBase64String(bytes) : null;
        }

        if (player.TryGetComponent<ClientEmoteComponent>(out var emotes))
        {
            data.AvailableEmotes = [.. emotes.AvailableEmotes.Select(EmoteTable.NameOf)];
            data.KeyboundEmotes = [.. emotes.KeyboundEmotes.Select(EmoteTable.NameOf)];
        }

        if (player.TryGetComponent<RecipeBookComponent>(out var recipeBook))
            data.KnownRecipes = [.. recipeBook.RecipeList
                .Select(entry => RecipeTable.TryGetByWireIndex(entry.Recipe, out var recipe) ? recipe.Name : null)
                .OfType<string>()];

        if (player.TryGetComponent<ClientSettingsComponent>(out var settings))
        {
            data.Settings = new PlayerSaveData.SettingsEntry
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
                MiniMap = settings.MiniMap,
                Hotbar = [.. settings.HotbarItems
                    .Select((item, i) => (item, i))
                    .Where(x => x.item is not null)
                    .Select(x => new PlayerSaveData.HotbarEntry
                    {
                        Slot = (uint)x.i / 2,
                        Hand = (uint)x.i % 2,
                        Item = ItemTable.NameOf(x.item!.Kind),
                        ItemUuid = x.item.ItemUuid,
                    })],
            };
        }

        if (player.TryGetComponent<SmoothedTransformComponent>(out var smoothedTransformComponent))
        {
            data.HasPosition = true;
            data.PositionMap = WorldMapLoader.MapFileName;
            data.PositionX = smoothedTransformComponent.Position.X;
            data.PositionY = smoothedTransformComponent.Position.Y;
            data.PositionZ = smoothedTransformComponent.Position.Z;
            data.YawDegrees = smoothedTransformComponent.YawDegrees;
        }

        if (player.TryGetComponent<ClientHealthComponent>(out var clientHealthComponent))
        {
            data.InitialHP = clientHealthComponent.InitialHP;
            data.WholeHearts = clientHealthComponent.WholeHearts;
            data.HalfHearts = clientHealthComponent.HalfHearts;
        }

        if (player.TryGetComponent<ClientJobRankComponent>(out var clientJobRankComponent))
        {
            data.Jobs = [.. clientJobRankComponent.JobList.Select(job => new PlayerSaveData.JobEntry
            {
                Name = job.Job,
                Rank = job.Rank,
                Experience = job.Experience,
                PrestigeRank = job.PrestigeRank,
                Available = job.NormalAvailable
            })];
            data.CompletedChallenges = clientJobRankComponent.CompletedJobChallengeList;
            data.ChallengeRefreshIndex = clientJobRankComponent.NormalJobChallengeRefreshData.ActiveRefreshIndex;
            var todoTasks = player.TryGetComponent<ClientTodoListComponent>(out var todoList) ? todoList.TodoTaskList : [];

            data.ActiveChallenges = [.. clientJobRankComponent.ActiveJobChallengeList.Select(challenge => new PlayerSaveData.ActiveChallengeEntry
            {
                Name = challenge.Name,
                Progress = challenge.Progress,
                TodoTimestamp = todoTasks.FirstOrDefault(task => task.JobChallenge.Equals(challenge.Name, StringComparison.OrdinalIgnoreCase))?.Timestamp ?? 0
            })];
        }

        if (player.TryGetComponent<ClientWalletComponent>(out var clientWalletComponent))
        {
            foreach (var currency in clientWalletComponent.WalletCurrency.CurrencyList)
            {
                if (!currency.NameHash.HasValue)
                    continue;

                data.Currencies.Add(new PlayerSaveData.CurrencyEntry
                {
                    NameHash = currency.NameHash.Value,
                    Value = currency.Value
                });
            }
        }

        if (player.TryGetComponent<ClientInventoryComponent>(out var clientInventoryComponent))
        {
            for (var slot = 0; slot < clientInventoryComponent.InventoryEntityList.Count; slot++)
            {
                var entityId = clientInventoryComponent.InventoryEntityList[slot];

                if (entityId == 0)
                    continue;

                if (!_entityManager.TryGetEntity(entityId, out var itemEntity))
                    continue;

                if (!itemEntity.TryGetComponent<InventoryItemComponent>(out var inventoryItemComponent))
                    continue;

                if (!inventoryItemComponent.InventorySlotData.Name.HasValue)
                    continue;

                data.Inventory.Add(new PlayerSaveData.InventoryEntry
                {
                    Slot = slot,
                    NameHash = inventoryItemComponent.InventorySlotData.Name.Value,
                    Count = inventoryItemComponent.InventorySlotData.Count,
                    ItemUuid = inventoryItemComponent.InventorySlotData.ItemUUID,
                    Materials = itemEntity.TryGetComponent<MaterialCompositionComponent>(out var composition) && composition.MaterialList.Length > 0
                        ? composition.MaterialList
                        : null
                });
            }
        }

        return data;
    }

    private void FillDefaultHotbar(ClientInventoryComponent inventory, ClientSettingsComponent settings)
    {
        for (var position = 0; position < Math.Min(inventory.InventoryEntityList.Count, (int)ClientEquippedItemComponent.SlotCount); position++)
        {
            var entityId = inventory.InventoryEntityList[position];

            if (entityId == 0
                || !_entityManager.TryGetEntity(entityId, out var itemEntity)
                || !itemEntity.TryGetComponent<InventoryItemComponent>(out var item)
                || item.InventorySlotData.Name is not { } crc)
                continue;

            var index = ItemTable.IndexOf(crc);
            var name = ItemTable.NameOf(index);

            (uint Slot, uint Hand)? place = name switch
            {
                _ when name.StartsWith("Weapon_", StringComparison.Ordinal) && name.Contains("_Sword_") => (0u, 1u),
                _ when name.Contains("_Pickaxe", StringComparison.Ordinal) => (1u, 1u),
                _ when name.EndsWith("_Torch", StringComparison.Ordinal) => (2u, 0u),
                "Dirt" => (3u, 1u),
                _ => null,
            };

            if (place is not { } p)
                continue;

            settings.SetHotbarItem(p.Slot, p.Hand, new InventoryItemRef
            {
                Kind = index,
                Amounts = itemEntity.TryGetComponent<MaterialCompositionComponent>(out var composition) ? [.. composition.MaterialList] : [],
                ItemUuid = Guid.TryParse(item.InventorySlotData.ItemUUID, out var uuid) ? uuid : null,
            });
        }
    }

    private void AttachHotbarMaterials(ClientInventoryComponent inventory, ClientSettingsComponent settings)
    {
        var materialsByUuid = new Dictionary<Guid, uint?[]>();

        foreach (var entityId in inventory.InventoryEntityList)
        {
            if (entityId != 0
                && _entityManager.TryGetEntity(entityId, out var itemEntity)
                && itemEntity.TryGetComponent<InventoryItemComponent>(out var item)
                && Guid.TryParse(item.InventorySlotData.ItemUUID, out var uuid)
                && itemEntity.TryGetComponent<MaterialCompositionComponent>(out var composition))
                materialsByUuid[uuid] = composition.MaterialList;
        }

        var items = (InventoryItemRef?[])settings.HotbarItems.Clone();

        for (var i = 0; i < items.Length; i++)
        {
            if (items[i] is { ItemUuid: { } uuid } item && materialsByUuid.TryGetValue(uuid, out var materials))
                item.Amounts = [.. materials];
        }

        settings.HotbarItems = items;
    }

    private void EquipArmourFromInventory(ClientInventoryComponent inventory, ClientEquippedItemComponent equipped)
    {
        PutArmourInItsSlots(inventory);

        var items = new ushort[ClientEquippedItemComponent.SlotCount];
        var uuids = new Guid?[ClientEquippedItemComponent.SlotCount];
        var materialLists = new uint?[ClientEquippedItemComponent.SlotCount][];

        for (var position = 0; position < Math.Min(inventory.InventoryEntityList.Count, (int)ClientEquippedItemComponent.SlotCount); position++)
        {
            var entityId = inventory.InventoryEntityList[position];

            if (ArmourSlotOf(entityId) is not { } slot || (int)slot != position
                || !_entityManager.TryGetEntity(entityId, out var itemEntity)
                || !itemEntity.TryGetComponent<InventoryItemComponent>(out var item)
                || item.InventorySlotData.Name is not { } crc)
                continue;

            items[position] = ItemTable.IndexOf(crc);
            uuids[position] = Guid.TryParse(item.InventorySlotData.ItemUUID, out var uuid) ? uuid : null;
            materialLists[position] = itemEntity.TryGetComponent<MaterialCompositionComponent>(out var composition) ? composition.MaterialList : [];
        }

        equipped.EquippedItemMaterials = materialLists;
        equipped.EquippedItems = items;
        equipped.EquippedItemUuids = uuids;
    }

    private void PutArmourInItsSlots(ClientInventoryComponent inventory)
    {
        var list = new List<int>(inventory.InventoryEntityList);

        for (var position = (int)ClientEquippedItemComponent.Slot.Head; position < Math.Min(list.Count, (int)ClientEquippedItemComponent.SlotCount); position++)
        {
            if (ArmourSlotOf(list[position]) is not { } slot || (int)slot == position || (int)slot >= list.Count
                || ArmourSlotOf(list[(int)slot]) == slot)
                continue;

            (list[(int)slot], list[position]) = (list[position], list[(int)slot]);
            position--;
        }

        inventory.InventoryEntityList = list;
    }

    private ClientEquippedItemComponent.Slot? ArmourSlotOf(int entityId)
    {
        if (entityId == 0
            || !_entityManager.TryGetEntity(entityId, out var itemEntity)
            || !itemEntity.TryGetComponent<InventoryItemComponent>(out var item)
            || item.InventorySlotData.Name is not { } crc)
            return null;

        var name = ItemTable.NameOf(ItemTable.IndexOf(crc));

        if (!name.StartsWith("Armour_", StringComparison.Ordinal))
            return null;

        return name switch
        {
            _ when name.EndsWith("_Head", StringComparison.Ordinal) => ClientEquippedItemComponent.Slot.Head,
            _ when name.EndsWith("_Torso", StringComparison.Ordinal) => ClientEquippedItemComponent.Slot.Torso,
            _ when name.EndsWith("_Legs", StringComparison.Ordinal) => ClientEquippedItemComponent.Slot.Legs,
            _ when name.EndsWith("_Arms", StringComparison.Ordinal) => ClientEquippedItemComponent.Slot.Arms,
            _ => null,
        };
    }

    private static readonly (string Currency, int Amount)[] StartingCurrencies = [("Coins", 10000), ("SkySagaHardCurrency", 500), ("CrafterCurrency", 1000), ("ExplorerCurrency", 1000), ("GladiatorCurrency", 1000)];

    private static readonly (string Item, uint Quantity, uint Price)[] GlobalShopStock =
    [
        ("Tool_Explorer_Generic_Pickaxe_1", 1, 250),
        ("Weapon_Forest_Royalist_Sword_0", 1, 400),
        ("Armour_Forest_Royalist_Heavy_1_Head", 1, 600),
        ("Camp_Fire", 1, 150),
        ("Tool_Explorer_Generic_Torch", 10, 50),
        ("Apple", 5, 20),
    ];

    private static List<ClientShopComponent.ShopItem> GlobalShopItems()
        => [.. GlobalShopStock.Select(entry => new ClientShopComponent.ShopItem
        {
            Item = new ClientShopComponent.ItemStack(ShopItemRef(entry.Item), entry.Quantity),
            Costs = [new ClientShopComponent.ItemStack(ShopItemRef("Coins"), entry.Price)],
        })];

    private static InventoryItemRef ShopItemRef(string name)
        => new()
        {
            Kind = ItemTable.IndexOf(name),
            Amounts = MaterialTable.TryGet(Util.ComputeCrc32(name), out var materials) ? [.. materials] : [],
        };

    private void EnsureItem(ClientInventoryComponent inventory, List<PlayerSaveData.InventoryEntry> saved, string itemName)
    {
        var crc = Util.ComputeCrc32(itemName);

        if (saved.Any(e => e.NameHash == crc))
            return;

        var free = inventory.InventoryEntityList.IndexOf(0, (int)ClientEquippedItemComponent.SlotCount);

        if (free >= 0)
            CreateAndEquipInventoryItem(inventory, free, crc);
    }

    private void CreateVault(ClientInventoryComponent inventory)
    {
        if (!_entityManager.TryCreateEntity("VaultInventory", out var vault)
            || !vault.TryGetComponent<ClientInventoryComponent>(out var vaultInventory))
            return;

        vaultInventory.MaxInventorySlots = VaultSlots;
        vaultInventory.InventoryEntityList = [.. Enumerable.Repeat(0, VaultSlots)];
        inventory.VaultInventoryEntity = vault.Id;
    }

    private void CreateAndEquipInventoryItem(ClientInventoryComponent inventory, int slot, string itemName, int count = 1)
        => CreateAndEquipInventoryItem(inventory, slot, Util.ComputeCrc32(itemName), count);

    private Entity? CreateAndEquipInventoryItem(ClientInventoryComponent inventory, int slot, uint nameHash, int count = 1, string? itemUuid = null, uint?[]? materials = null)
    {
        var item = CreateInventoryItem(nameHash, count, itemUuid, materials);

        if (item is not null)
            inventory.InventoryEntityList[slot] = item.Id;

        return item;
    }

    public Entity? CreateInventoryItem(uint nameHash, int count, string? itemUuid = null, uint?[]? materialOverrides = null)
        => CreateInventoryItem(_entityManager, nameHash, count, itemUuid, materialOverrides);

    public static Entity? CreateInventoryItem(IMapEntityManager entityManager, uint nameHash, int count, string? itemUuid = null, uint?[]? materialOverrides = null)
    {
        var durable = DurabilityTable.TryGetMax(nameHash, out var durabilityMax);
        var materialsAllowed = Environment.GetEnvironmentVariable("SKYSAGA_NO_MATERIALS") != "1";
        var materials = MaterialTable.TryGet(nameHash, out var defaults) ? defaults : [];

        if (materialOverrides is not null)
            materials = WithOverrides(materials, materialOverrides);

        var hasMaterials = materials.Length > 0 && materialsAllowed;

        var entityType = (durable, hasMaterials) switch
        {
            (true, true) => "MaterialDurableInventoryItem",
            (false, true) => "MaterialBasedInventoryItem",
            (true, false) => "DurableInventoryItem",
            _ => "BasicInventoryItem"
        };

        if (entityManager.TryCreateEntity(entityType, out var item))
        {
            if (hasMaterials && item.TryGetComponent<MaterialCompositionComponent>(out var composition))
                composition.MaterialList = materials;

            if (ItemTagTable.TryGet(nameHash, out var itemTags) && item.TryGetComponent<TagsComponent>(out var tagsComponent))
                tagsComponent.TagList = itemTags;

            if (durable && item.TryGetComponent<ClientDurabilityComponent>(out var durability))
            {
                durability.DurabilityMax = durabilityMax;
                durability.Durability = durabilityMax;
            }

            if (item.TryGetComponent<InventoryItemComponent>(out var inventoryItemComponent))
            {
                inventoryItemComponent.InventorySlotData.Name = nameHash;
                inventoryItemComponent.InventorySlotData.Count = count;
                inventoryItemComponent.InventorySlotData.ItemUUID = itemUuid ?? Util.NewGuid();
            }
        }

        return item;
    }

    private static uint?[] WithOverrides(uint?[] materials, uint?[] overrides)
    {
        var length = Math.Max(materials.Length, Array.FindLastIndex(overrides, material => material is not null) + 1);
        var result = new uint?[length];

        for (var i = 0; i < length; i++)
            result[i] = i < overrides.Length && overrides[i] is { } material ? material : i < materials.Length ? materials[i] : null;

        return result;
    }
}
