using System.Linq;
namespace SkySaga.Game.Managers.Player;

public class PlayerInitializer(IWorldManager worldManager)
{
    private readonly IWorldManager _worldManager = worldManager ?? throw new ArgumentNullException(nameof(worldManager));
    private readonly Random _random = new();
    private IMapEntityManager _entityManager => _worldManager.EntityManager;









    public void InitializePlayer(Entity player)
    {
        ArgumentNullException.ThrowIfNull(player);

        var save = PlayerSaveStore.Load();

        if (player.TryGetComponent<ClientPlayernameComponent>(out var clientPlayernameComponent))
            clientPlayernameComponent.Playername = !string.IsNullOrEmpty(save?.Playername)
                ? save.Playername
                : Util.CharacterName();

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
            settings.CameraFlag = saved.CameraFlag;
            settings.CameraState = saved.CameraState;
            settings.ControlFlag = saved.ControlFlag;
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





            // A saved position inside a solid voxel is worse than none: the camera sits inside the
            // terrain and the client renders black, with only the odd purple face edge visible.
            // That happened for real - the save held (0, 0, 0) while the map's ground occupies
            // y = 0..1, because EntityMoved is not handled yet and the position never updated before
            // being written back.
            var savedPosition = save is { HasPosition: true }
                ? new Vector3(save.PositionX, save.PositionY, save.PositionZ)
                : (Vector3?)null;

            if (savedPosition is { } position && SpawnFinder.IsSolidAt(_worldManager.ChunkManager, position))
            {
                Console.WriteLine($"[PlayerInitializer] Saved position {position} is inside a solid voxel - ignoring it.");
                savedPosition = null;
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

            // SKYSAGA_SPAWN_Y overrides the spawn height, for testing what the camera sees from above.
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

            // SKYSAGA_NO_INVENTORY=1: create no item entities (test switch). The FTL index assert at
            // 0x00BA8306 it used to avoid was InventorySlotData sending the item name CRC where the
            // client reads an item-table index (fixed 2026-10-12, see InventorySlotData).
            if (Environment.GetEnvironmentVariable("SKYSAGA_NO_INVENTORY") == "1")
            {
            }
            else if (save is { Inventory.Count: > 0 })
            {
                foreach (var entry in save.Inventory)
                {
                    if (entry.Slot < 0 || entry.Slot >= clientInventoryComponent.MaxInventorySlots)
                        continue;

                    CreateAndEquipInventoryItem(clientInventoryComponent, entry.Slot, entry.NameHash, entry.Count);
                }
            }
            else
            {
                // Names must exist in GeoData Resources (Data/Resources.txt); an unknown name maps to
                // item index 0 and shows as an empty placeholder.
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

                CreateAndEquipInventoryItem(clientInventoryComponent, 17, "Stone", 10);
                CreateAndEquipInventoryItem(clientInventoryComponent, 18, "Clay", 10);

                CreateAndEquipInventoryItem(clientInventoryComponent, 19, "Flint_and_Tinder", 1);

            }
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
                CameraFlag = settings.CameraFlag,
                CameraState = settings.CameraState,
                ControlFlag = settings.ControlFlag,
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
                    Count = inventoryItemComponent.InventorySlotData.Count
                });
            }
        }

        return data;
    }




    private void CreateAndEquipInventoryItem(ClientInventoryComponent inventory, int slot, string itemName, int count = 1)
        => CreateAndEquipInventoryItem(inventory, slot, Util.ComputeCrc32(itemName), count);





    private void CreateAndEquipInventoryItem(ClientInventoryComponent inventory, int slot, uint nameHash, int count = 1)
    {
        if (_entityManager.TryCreateEntity("BasicInventoryItem", out var item))
        {
            if (item.TryGetComponent<InventoryItemComponent>(out var inventoryItemComponent))
            {
                inventoryItemComponent.InventorySlotData.Name = nameHash;
                inventoryItemComponent.InventorySlotData.Count = count;
                inventoryItemComponent.InventorySlotData.ItemUUID = Util.NewGuid();
            }

            inventory.InventoryEntityList[slot] = item.Id;
        }
    }
}
