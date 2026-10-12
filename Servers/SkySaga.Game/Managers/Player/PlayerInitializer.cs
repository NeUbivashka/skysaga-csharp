using System.Linq;
namespace SkySaga.Game.Managers.Player;

public class PlayerInitializer(IWorldManager worldManager)
{
    private const uint ForestMasterSigil = 10;
    private static readonly uint[] StarterSigils = [2, 8, ForestMasterSigil, 14];

    private readonly IWorldManager _worldManager = worldManager ?? throw new ArgumentNullException(nameof(worldManager));
    private readonly Random _random = new();
    private IMapEntityManager _entityManager => _worldManager.EntityManager;

    public void InitializePlayer(Entity player)
    {
        ArgumentNullException.ThrowIfNull(player);

        var save = PlayerSaveStore.Load();

        if (player.TryGetComponent<ClientPlayernameComponent>(out var clientPlayernameComponent))
        {
            clientPlayernameComponent.Playername = !string.IsNullOrEmpty(save?.Playername)
                ? save.Playername
                : Util.CharacterName();

            clientPlayernameComponent.AvailablePlayerSigilList = [.. StarterSigils.Select(index => (index, 0u, 0u))];
            clientPlayernameComponent.CurrentSigil = ForestMasterSigil;
        }

        if (player.TryGetComponent<TagsComponent>(out var tags))
            tags.TagList = [Util.ComputeCrc32("Player")];

        if (player.TryGetComponent<ClientCharacterCustomisationComponent>(out var customisation) && save is not null)
        {
            customisation.IsCharacterCustomised = save.IsCharacterCustomised;

            if (!string.IsNullOrEmpty(save.CustomisationData))
                customisation.CustomisationData = Convert.FromBase64String(save.CustomisationData);
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
            var savedPosition = save is { HasPosition: true }
                ? new Vector3(save.PositionX, save.PositionY, save.PositionZ)
                : (Vector3?)null;

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
                smoothedTransformComponent.Position = _worldManager.PlayerSpawnPosition
                    ?? SpawnFinder.FindSpawnNearPoint(
                        _worldManager.ChunkManager,
                        _worldManager.AirshipPosition,
                        5,
                        10,
                        _worldManager.Definition.MapSizeChunks,
                        _random);
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
        }

        if (player.TryGetComponent<ClientWalletComponent>(out var clientWalletComponent))
        {
            if (save is { Currencies.Count: > 0 })
            {
                foreach (var currency in save.Currencies)
                {
                    clientWalletComponent.Currency.CurrencyList.Add(new WalletData.CurrencyData
                    {
                        NameHash = currency.NameHash,
                        Value = currency.Value
                    });
                }
            }
            else
            {
                clientWalletComponent.Currency.CurrencyList.Add(new WalletData.CurrencyData
                {
                    NameHash = Util.ComputeCrc32("Life_Ticket"),
                    Value = 69
                });

                clientWalletComponent.Currency.CurrencyList.Add(new WalletData.CurrencyData
                {
                    NameHash = Util.ComputeCrc32("Portal_Ticket"),
                    Value = 420
                });
            }
        }

        if (player.TryGetComponent<ClientInventoryComponent>(out var clientInventoryComponent))
        {
            clientInventoryComponent.MaxInventorySlots = 45;

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

                    CreateAndEquipInventoryItem(clientInventoryComponent, entry.Slot, entry.NameHash, entry.Count, entry.ItemUuid);
                }

                EnsureItem(clientInventoryComponent, save.Inventory, "Tool_Explorer_Generic_Pickaxe_1");
            }
            else
            {
                CreateAndEquipInventoryItem(clientInventoryComponent, 2, "Armour_Explorer_Generic_Medium_1_Head");
                CreateAndEquipInventoryItem(clientInventoryComponent, 3, "Armour_Explorer_Generic_Medium_1_Torso");
                CreateAndEquipInventoryItem(clientInventoryComponent, 4, "Armour_Explorer_Generic_Medium_1_Arms");
                CreateAndEquipInventoryItem(clientInventoryComponent, 5, "Armour_Explorer_Generic_Medium_1_Legs");

                CreateAndEquipInventoryItem(clientInventoryComponent, 9, "Dirt", 10);
                CreateAndEquipInventoryItem(clientInventoryComponent, 10, "Tool_Explorer_Generic_Torch", 10);
                CreateAndEquipInventoryItem(clientInventoryComponent, 11, "Anvil_2x1x1", 1);
                CreateAndEquipInventoryItem(clientInventoryComponent, 13, "Camp_Fire", 1);
                CreateAndEquipInventoryItem(clientInventoryComponent, 14, "Weapon_Forest_Royalist_Dagger_0", 1);
                CreateAndEquipInventoryItem(clientInventoryComponent, 15, "Weapon_Forest_Royalist_Sword_0", 1);
                CreateAndEquipInventoryItem(clientInventoryComponent, 16, "Tool_Explorer_Generic_Pickaxe_0", 1);
                CreateAndEquipInventoryItem(clientInventoryComponent, 18, "Tool_Explorer_Generic_Pickaxe_1", 1);

                CreateAndEquipInventoryItem(clientInventoryComponent, 17, "Stone", 10);
                CreateAndEquipInventoryItem(clientInventoryComponent, 18, "Clay", 10);

                CreateAndEquipInventoryItem(clientInventoryComponent, 19, "Flint_and_Tinder", 1);
            }

            if (player.TryGetComponent<ClientEquippedItemComponent>(out var equipped))
                EquipArmourFromInventory(clientInventoryComponent, equipped);

            if (player.TryGetComponent<ClientSettingsComponent>(out var hotbarSettings) && save?.Settings?.Hotbar is null)
                FillDefaultHotbar(clientInventoryComponent, hotbarSettings);
        }

        if (save is null)
            PlayerSaveStore.Save(CaptureSaveData(player));
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

        if (player.TryGetComponent<ClientWalletComponent>(out var clientWalletComponent))
        {
            foreach (var currency in clientWalletComponent.Currency.CurrencyList)
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
                    ItemUuid = inventoryItemComponent.InventorySlotData.ItemUUID
                });
            }
        }

        return data;
    }

    private void FillDefaultHotbar(ClientInventoryComponent inventory, ClientSettingsComponent settings)
    {
        foreach (var entityId in inventory.InventoryEntityList)
        {
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
                ItemUuid = Guid.TryParse(item.InventorySlotData.ItemUUID, out var uuid) ? uuid : null,
            });
        }
    }

    private void EquipArmourFromInventory(ClientInventoryComponent inventory, ClientEquippedItemComponent equipped)
    {
        var items = new ushort[ClientEquippedItemComponent.SlotCount];
        var uuids = new Guid?[ClientEquippedItemComponent.SlotCount];

        foreach (var entityId in inventory.InventoryEntityList)
        {
            if (entityId == 0
                || !_entityManager.TryGetEntity(entityId, out var itemEntity)
                || !itemEntity.TryGetComponent<InventoryItemComponent>(out var item)
                || item.InventorySlotData.Name is not { } crc)
                continue;

            var index = ItemTable.IndexOf(crc);
            var name = ItemTable.NameOf(index);

            if (!name.StartsWith("Armour_", StringComparison.Ordinal))
                continue;

            ClientEquippedItemComponent.Slot? slot = name switch
            {
                _ when name.EndsWith("_Head", StringComparison.Ordinal) => ClientEquippedItemComponent.Slot.Head,
                _ when name.EndsWith("_Torso", StringComparison.Ordinal) => ClientEquippedItemComponent.Slot.Torso,
                _ when name.EndsWith("_Legs", StringComparison.Ordinal) => ClientEquippedItemComponent.Slot.Legs,
                _ when name.EndsWith("_Arms", StringComparison.Ordinal) => ClientEquippedItemComponent.Slot.Arms,
                _ => null,
            };

            if (slot is null)
                continue;

            items[(int)slot] = index;
            uuids[(int)slot] = Guid.TryParse(item.InventorySlotData.ItemUUID, out var uuid) ? uuid : null;
        }

        equipped.EquippedItems = items;
        equipped.EquippedItemUuids = uuids;
    }

    private void EnsureItem(ClientInventoryComponent inventory, List<PlayerSaveData.InventoryEntry> saved, string itemName)
    {
        var crc = Util.ComputeCrc32(itemName);

        if (saved.Any(e => e.NameHash == crc))
            return;

        var free = inventory.InventoryEntityList.IndexOf(0);

        if (free >= 0)
            CreateAndEquipInventoryItem(inventory, free, crc);
    }

    private void CreateAndEquipInventoryItem(ClientInventoryComponent inventory, int slot, string itemName, int count = 1)
        => CreateAndEquipInventoryItem(inventory, slot, Util.ComputeCrc32(itemName), count);

    private void CreateAndEquipInventoryItem(ClientInventoryComponent inventory, int slot, uint nameHash, int count = 1, string? itemUuid = null)
    {
        var durable = DurabilityTable.TryGetMax(nameHash, out var durabilityMax);
        var hasMaterials = MaterialTable.TryGet(nameHash, out var materials);

        var entityType = (durable, hasMaterials) switch
        {
            (true, true) => "MaterialDurableInventoryItem",
            (false, true) => "MaterialBasedInventoryItem",
            (true, false) => "DurableInventoryItem",
            _ => "BasicInventoryItem"
        };

        if (_entityManager.TryCreateEntity(entityType, out var item))
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

            inventory.InventoryEntityList[slot] = item.Id;
        }
    }
}
