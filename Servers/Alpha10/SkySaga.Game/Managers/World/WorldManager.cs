using SkySaga.Game.Database;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace SkySaga.Game.Managers.World;

public sealed class WorldManager : IWorldManager, IDisposable
{
    private readonly MapChunkManager _chunkManager;
    private readonly Random _random = new(1337);
    private FileSystemWatcher? _mapFileWatcher;
    private System.Threading.Timer? _reloadDebounceTimer;
    private readonly object _reloadLock = new();

    public WorldInfo Info { get; }
    public IMapEntityManager EntityManager { get; }
    public IMapChunkManager ChunkManager => _chunkManager;
    public MapDefinition Definition { get; set; }
    public Entity? Airship { get; private set; }

    public record StoredItem(uint ItemCrc, int Count, uint?[] Materials);

    public record PlacedDevice(uint ItemCrc, int X, int Y, int Z, Guid? Owner, uint?[] Materials)
    {
        public List<StoredItem?> Items { get; set; } = [];
    }

    private readonly Dictionary<int, PlacedDevice> _deviceEntities = [];

    private readonly List<PlacedDevice> _devices = [];

    private const int CheckpointSearchRadius = 4;
    private const int CheckpointClearance = 2;

    private readonly Dictionary<Guid, float[]> _checkpoints;
    private readonly object _devicesLock = new();

    private readonly Dictionary<int, (Vector3 Position, float? Yaw, string? SpawnParams)> _mapCreatureSpawns = [];

    private const int LandingSearchRadius = 16;

    public System.Collections.Concurrent.ConcurrentDictionary<Vector3Int, float> VoxelDamage { get; } = new();

    public WorldManager(WorldInfo info, PlayerConnectionManager playerConnectionManager)
    {
        ArgumentNullException.ThrowIfNull(playerConnectionManager);

        Info = info;
        EntityManager = new EntityManager();
        _chunkManager = new MapChunkManager(info, playerConnectionManager);
        _checkpoints = info.IsPersistent ? WorldStore.LoadCheckpoints(info.Id) : [];

        InitializeWorld();
        SetupMapFileWatcher();
    }

    private void SetupMapFileWatcher()
    {
        if (Info.TemplateVersion is not null)
            return;

        try
        {
            string fullPath = TemplateStore.FilePath(Info.Template);
            string? dir = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(dir))
                return;

            Directory.CreateDirectory(dir);

            _mapFileWatcher = new FileSystemWatcher(dir)
            {
                Filter = Path.GetFileName(fullPath) + "*",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            _mapFileWatcher.Changed += OnMapFileChanged;
            _mapFileWatcher.Created += OnMapFileChanged;
            _mapFileWatcher.Renamed += OnMapFileChanged;

            Console.WriteLine($"[WorldManager] {Info.Kind} {Info.Id} watches {fullPath}: saving the template reloads this world.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WorldManager] Could not set up map file watcher (auto-reload disabled): {ex.Message}");
        }
    }

    private void OnMapFileChanged(object sender, FileSystemEventArgs e)
    {
        _reloadDebounceTimer?.Dispose();
        _reloadDebounceTimer = new System.Threading.Timer(_ =>
        {
            lock (_reloadLock)
            {
                Console.WriteLine($"[WorldManager] {Info.Template} changed on disk - reloading {Info.Kind} {Info.Id}...");
                try
                {
                    Reset();
                    Console.WriteLine("[WorldManager] Map auto-reload complete.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WorldManager] Map auto-reload failed: {ex.Message}");
                }
            }
        }, null, 400, System.Threading.Timeout.Infinite);
    }

    public void Dispose()
    {
        _mapFileWatcher?.Dispose();
        _reloadDebounceTimer?.Dispose();
    }

    [MemberNotNull(nameof(Definition))]
    private void InitializeWorld()
    {
        const bool meandrosEnabled = false;
        if (meandrosEnabled && MeandrosSceneLoader.TryLoad(ChunkManager, out var meandrosDefinition, out var meandrosEntities) && meandrosDefinition is not null)
        {
            Definition = meandrosDefinition;

            SpawnEntitiesFromMap(meandrosEntities, out var meandrosAirshipPosition);

            SpawnAirship(meandrosAirshipPosition);
            ChunkManager.LoadSavedEdits();
            PlaceLinkedVoxels(Airship);

            return;
        }

        if (WorldMapLoader.TryLoad(ChunkManager, Info, out var loadedDefinition, out var loadedEntities) && loadedDefinition is not null)
        {
            Definition = loadedDefinition;

            SpawnEntitiesFromMap(loadedEntities, out var mapAirshipPosition);

            SpawnAirship(mapAirshipPosition);
        }
        else
        {
            Console.WriteLine($"[WorldManager] The template {Info.Template} could not be loaded - starting an empty world.");

            Definition = new MapDefinition
            {
                MapSizeChunks = new Vector3Int(4, 4, 4),

                Spec = MapSpec.Ftue(seed: 1337),
                GameMode = 1,
                AdventureType = "Adventure",

                MapFileName = "TestMap"
            };

            SpawnAirship(null);
        }

        ChunkManager.LoadSavedEdits();
        LoadDevices();

        foreach (var entity in EntityManager.Entities.ToList())
            PlaceLinkedVoxels(entity);

        SpawnTimeOfDay();
        SpawnWorldComposition();
    }

    private void SpawnAirship(Vector3? mapPosition)
    {
        var position = mapPosition
            ?? SpawnFinder.FindEdgeSpawnPoint(ChunkManager, Definition.MapSizeChunks, _random)
            ?? SpawnFinder.FindSafeSpawnPoint(ChunkManager, Definition.MapSizeChunks);

        if (!EntityManager.TryCreateEntity("Airship", out var airShip))
            return;

        Airship = airShip;

        if (airShip.TryGetComponent<TransformComponent>(out var transformComponent))
            transformComponent.Position = position;

        if (airShip.TryGetComponent<ClientPickupComponent>(out var pickup))
        {
            var nameHash = Util.ComputeCrc32("Airship");

            pickup.InventoryItemSpec = new InventoryItemRef
            {
                Kind = (ushort)ItemTable.IndexOf(nameHash),
                Amounts = MaterialTable.TryGet(nameHash, out var materials) ? [.. materials] : []
            };
        }
    }

    private void SpawnWorldComposition()
    {
        if (EntityManager.TryCreateEntity("WorldComposition", out var worldComposition) &&
            worldComposition.TryGetComponent<ClientWorldCompositionComponent>(out var composition))
        {
            composition.PaletteCrc =
                uint.TryParse(Environment.GetEnvironmentVariable("SKYSAGA_PALETTE"), out var palette)
                    ? palette : Definition.Spec.Palette;
            composition.TerrainType = TerrainType.Sky;
            composition.MinWalkableYPosition = 0;
        }
    }

    private const int StartTimeOfDay = 10 * TimeOfDayComponent.DayLength / 24;
    private const int DayNightCycleDuration = 30 * 64;

    private void SpawnTimeOfDay()
    {
        if (EntityManager.TryCreateEntity("TimeOfDay", out var timeOfDay) &&
            timeOfDay.TryGetComponent<ClientTimeOfDayComponent>(out var clientTimeOfDayComponent))
        {
            clientTimeOfDayComponent.StartTimeOfDay =
                int.TryParse(Environment.GetEnvironmentVariable("SKYSAGA_TIME_OF_DAY"), out var tod) ? tod : StartTimeOfDay;
            clientTimeOfDayComponent.FixedTimeOfDay = false;
            clientTimeOfDayComponent.DayNightCycleDuration = DayNightCycleDuration;
            clientTimeOfDayComponent.RealWorldStartTime = RakNet.RakNet.GetTime();
            clientTimeOfDayComponent.TimeStretch = Environment.GetEnvironmentVariable("SKYSAGA_TIME_FROZEN") == "1" ? 0 : TimeOfDayComponent.NormalStretch;

            clientTimeOfDayComponent.TimeOfDayOffset = clientTimeOfDayComponent.StartTimeOfDay;
        }
    }

    private void PlaceLinkedVoxels(Entity? entity)
    {
        if (entity is null
            || !entity.TryGetComponent<ClientVoxelLinkComponent>(out var voxelLink)
            || !entity.TryGetComponent<TransformComponent>(out var transform))
            return;

        foreach (var (position, voxelType) in voxelLink.GetWorldVoxels(transform.Position, transform.YawDegrees))
        {
            var chunkPosition = new Vector3Int(FloorDiv(position.X, ChunkSize), FloorDiv(position.Y, ChunkSize), FloorDiv(position.Z, ChunkSize));
            var localPosition = new Vector3Int(position.X - chunkPosition.X * ChunkSize, position.Y - chunkPosition.Y * ChunkSize, position.Z - chunkPosition.Z * ChunkSize);

            ChunkManager.GetOrCreateChunk(chunkPosition).SetVoxel(localPosition, voxelType);
        }

        Console.WriteLine($"[WorldManager] Placed {voxelLink.Voxels.Count} linked voxels of {entity.Name}#{entity.Id}.");
    }

    public bool CanPlaceDevice(DeviceInfo device, Vector3Int voxel)
    {
        var origin = DeviceTable.Origin(device, voxel);
        var originVoxel = new Vector3Int((int)MathF.Floor(origin.X), (int)MathF.Floor(origin.Y), (int)MathF.Floor(origin.Z));

        foreach (var cell in device.Cells)
        {
            var position = originVoxel + cell;
            var chunk = new Vector3Int(FloorDiv(position.X, ChunkSize), FloorDiv(position.Y, ChunkSize), FloorDiv(position.Z, ChunkSize));
            var local = new Vector3Int(position.X - chunk.X * ChunkSize, position.Y - chunk.Y * ChunkSize, position.Z - chunk.Z * ChunkSize);

            if (ChunkManager.GetVoxel(chunk, local) != WorldChunk.Air)
                return false;
        }

        return true;
    }

    public List<Entity> PlaceDevice(uint itemCrc, DeviceInfo device, Vector3Int voxel, Guid? owner, uint?[] materials)
    {
        if (!CanPlaceDevice(device, voxel))
            return [];

        var placed = new PlacedDevice(itemCrc, voxel.X, voxel.Y, voxel.Z, owner, materials);
        var entities = SpawnDevice(placed, device);

        if (entities.Count == 0)
            return entities;

        if (entities[^1].TryGetComponent<ClientVoxelLinkComponent>(out var voxelLink)
            && entities[^1].TryGetComponent<TransformComponent>(out var transform))
        {
            foreach (var (position, voxelType) in voxelLink.GetWorldVoxels(transform.Position, transform.YawDegrees))
            {
                var chunk = new Vector3Int(FloorDiv(position.X, ChunkSize), FloorDiv(position.Y, ChunkSize), FloorDiv(position.Z, ChunkSize));
                var local = new Vector3Int(position.X - chunk.X * ChunkSize, position.Y - chunk.Y * ChunkSize, position.Z - chunk.Z * ChunkSize);
                ChunkManager.SetVoxel(chunk, local, voxelType);
            }
        }

        lock (_devicesLock)
        {
            _devices.Add(placed);
            SaveDevices();
        }

        return entities;
    }

    public List<int> RemoveDevice(Entity entity, bool placedOnly, out PlacedDevice? placed)
    {
        placed = null;

        if (!DeviceTable.TryGetByEntity(entity.Name, out _))
            return [];

        lock (_devicesLock)
        {
            if (placedOnly && !_deviceEntities.ContainsKey(entity.Id))
                return [];
        }

        if (entity.TryGetComponent<ClientVoxelLinkComponent>(out var voxelLink)
            && entity.TryGetComponent<TransformComponent>(out var transform))
        {
            foreach (var (position, _) in voxelLink.GetWorldVoxels(transform.Position, transform.YawDegrees))
            {
                var chunk = new Vector3Int(FloorDiv(position.X, ChunkSize), FloorDiv(position.Y, ChunkSize), FloorDiv(position.Z, ChunkSize));
                var local = new Vector3Int(position.X - chunk.X * ChunkSize, position.Y - chunk.Y * ChunkSize, position.Z - chunk.Z * ChunkSize);
                ChunkManager.SetVoxel(chunk, local, WorldChunk.Air);
            }
        }

        List<int> removed = entity.TryGetComponent<ClientInventoryComponent>(out var inventory)
            ? [.. inventory.InventoryEntityList.Where(id => id != 0)]
            : [];

        foreach (var id in removed)
            EntityManager.RemoveEntity(id);

        lock (_devicesLock)
        {
            if (_deviceEntities.Remove(entity.Id, out var found))
            {
                placed = found;
                _devices.RemoveAll(device => ReferenceEquals(device, found));
                SaveDevices();
            }
        }

        EntityManager.RemoveEntity(entity.Id);
        removed.Add(entity.Id);
        return removed;
    }

    public void DeviceInventoryChanged(Entity entity)
    {
        lock (_devicesLock)
        {
            if (!_deviceEntities.TryGetValue(entity.Id, out var placed)
                || !entity.TryGetComponent<ClientInventoryComponent>(out var inventory))
                return;

            placed.Items = [.. inventory.InventoryEntityList.Select(id =>
                id != 0 && EntityManager.TryGetEntity(id, out var item)
                    && item.TryGetComponent<InventoryItemComponent>(out var component)
                    && component.InventorySlotData.Name is uint crc
                    ? new StoredItem(crc, component.InventorySlotData.Count, ResourcePickups.MaterialsOf(item))
                    : null)];

            SaveDevices();
        }
    }

    private List<Entity> SpawnDevice(PlacedDevice placed, DeviceInfo device)
    {
        var entities = SpawnCreature(device.Entity, DeviceTable.Origin(device, new Vector3Int(placed.X, placed.Y, placed.Z)));

        if (entities.Count == 0)
            return entities;

        if (entities[^1].TryGetComponent<ClientOwnerComponent>(out var owner))
            owner.Owner = placed.Owner;

        SetDeviceItemSpec(entities[^1], placed.ItemCrc, placed.Materials);
        FillLinkedVoxels(entities[^1], device);
        _deviceEntities[entities[^1].Id] = placed;

        if (placed.Items.Count > 0 && entities[^1].TryGetComponent<ClientInventoryComponent>(out var inventory))
        {
            var items = placed.Items
                .Select(stored => stored is null ? 0
                    : PlayerInitializer.CreateInventoryItem(EntityManager, stored.ItemCrc, stored.Count, materialOverrides: stored.Materials)?.Id ?? 0)
                .Take(inventory.MaxInventorySlots)
                .ToList();

            inventory.InventoryEntityList = [.. items, .. Enumerable.Repeat(0, Math.Max(0, inventory.MaxInventorySlots - items.Count))];
        }

        return entities;
    }

    private static void FillLinkedVoxels(Entity entity, DeviceInfo device)
    {
        if (entity.TryGetComponent<ClientVoxelLinkComponent>(out var voxelLink)
            && voxelLink.Voxels.Count == 0
            && voxelLink.VoxelType is byte voxelType)
            voxelLink.Voxels = [.. device.Cells.Select(cell => new LinkedVoxel(cell, voxelType))];
    }

    private static void SetDeviceItemSpec(Entity entity, uint itemCrc, uint?[] materials)
    {
        if (entity.TryGetComponent<ClientPerUserInventoryStoreComponent>(out var store))
            store.IsLootFountain = false;

        if (entity.TryGetComponent<ClientPickupComponent>(out var pickup))
            pickup.InventoryItemSpec = new InventoryItemRef
            {
                Kind = (ushort)ItemTable.IndexOf(itemCrc),
                Amounts = [.. materials]
            };
    }

    private void LoadDevices()
    {
        lock (_devicesLock)
        {
            _devices.Clear();
            _deviceEntities.Clear();

            if (Info.IsPersistent)
                _devices.AddRange(WorldStore.LoadDevices(Info.Id).Select(FromRecord));

            foreach (var placed in _devices)
                if (DeviceTable.TryGet(placed.ItemCrc, out var device))
                    SpawnDevice(placed, device);

            Console.WriteLine($"[WorldManager] Loaded {_devices.Count} placed devices.");
        }
    }

    private void SaveDevices()
    {
        if (Info.IsPersistent)
            WorldStore.SaveDevices(Info.Id, _devices.Select(ToRecord));
    }

    private static Data.Entities.PlacedDevice ToRecord(PlacedDevice device) => new()
    {
        ItemCrc = device.ItemCrc,
        X = device.X,
        Y = device.Y,
        Z = device.Z,
        OwnerCharacterId = device.Owner,
        Materials = [.. device.Materials.Select(m => (long?)m)],
        Items = JsonSerializer.Serialize(device.Items)
    };

    private static PlacedDevice FromRecord(Data.Entities.PlacedDevice record)
        => new((uint)record.ItemCrc, record.X, record.Y, record.Z, record.OwnerCharacterId, [.. record.Materials.Select(m => (uint?)m)])
        {
            Items = JsonSerializer.Deserialize<List<StoredItem?>>(record.Items) ?? []
        };

    private const int ChunkSize = 32;

    private static int FloorDiv(int value, int divisor) => (int)Math.Floor(value / (double)divisor);

    public (Vector3 Position, float YawDegrees) GetPlayerLandingPoint()
    {
        if (Airship is null || !Airship.TryGetComponent<TransformComponent>(out var transform))
            return (SpawnFinder.FindSafeSpawnPoint(ChunkManager, Definition.MapSizeChunks), 0f);

        var (point, yaw) = Airship.TryGetComponent<LandingPadComponent>(out var landingPad)
            ? landingPad.GetLandingPoint(transform.Position, transform.YawDegrees)
            : (transform.Position, transform.YawDegrees);

        var standing = SpawnFinder.FindStandingPoint(ChunkManager, point, Definition.MapSizeChunks, LandingSearchRadius)
            ?? SpawnFinder.FindSafeSpawnPoint(ChunkManager, Definition.MapSizeChunks);

        return (standing, yaw);
    }

    public (Vector3 Position, float YawDegrees) GetRespawnPoint(Guid? character)
    {
        float[]? checkpoint;

        lock (_checkpoints)
            checkpoint = character is Guid id && _checkpoints.TryGetValue(id, out var saved) ? saved : null;

        if (checkpoint is not { Length: 3 })
            return GetPlayerLandingPoint();

        var point = new Vector3(checkpoint[0], checkpoint[1], checkpoint[2]);
        var standing = SpawnFinder.FindStandingPoint(ChunkManager, point, Definition.MapSizeChunks, CheckpointSearchRadius, CheckpointClearance);

        return standing is Vector3 found ? (found, 0f) : GetPlayerLandingPoint();
    }

    public void SetCheckpoint(Guid character, Entity checkpoint)
    {
        if (!checkpoint.TryGetComponent<TransformComponent>(out var transform))
            return;

        lock (_checkpoints)
            _checkpoints[character] = [transform.Position.X, transform.Position.Y, transform.Position.Z];

        if (Info.IsPersistent)
            WorldStore.SaveCheckpoint(Info.Id, character, transform.Position.X, transform.Position.Y, transform.Position.Z);

        Console.WriteLine($"[Checkpoint] {character} -> {checkpoint.Name}#{checkpoint.Id} at {transform.Position}");
    }

    private void SpawnEntitiesFromMap(List<WorldMapEntityData> entities, out Vector3? airshipPosition)
    {
        airshipPosition = null;

        int spawned = 0;

        foreach (var spec in entities)
        {
            if (string.IsNullOrWhiteSpace(spec.Name))
                continue;

            var position = DeviceTable.TryGetByEntity(spec.Name, out var mapDevice)
                ? DeviceTable.Origin(mapDevice, new Vector3Int(spec.X, spec.Y, spec.Z))
                : new Vector3(spec.X + 0.5f, spec.Y, spec.Z + 0.5f);

            if (string.Equals(spec.Name, "Airship", StringComparison.OrdinalIgnoreCase))
            {
                airshipPosition = position;
                continue;
            }

            if (string.Equals(spec.Name, "TimeOfDay", StringComparison.OrdinalIgnoreCase))
                continue;

            if (string.Equals(spec.Name, "Player", StringComparison.OrdinalIgnoreCase))
                continue;

            var creature = SpawnCreature(spec.Name, position);

            if (spec.Items is { Count: > 0 } && creature.Count > 0)
                FillInventory(creature[^1], spec.Items);

            if (spec.MentalState is { Length: > 0 } && creature.Count > 0)
                SetMentalState(creature[^1], spec.MentalState);

            if (spec.Yaw is float yaw && creature.Count > 0)
                SetYaw(creature[^1], yaw);

            if (creature.Count > 0 && DeviceTable.TryGetByEntity(spec.Name, out var linkedDevice))
            {
                FillLinkedVoxels(creature[^1], linkedDevice);

                var nameHash = Util.ComputeCrc32(spec.Name);
                SetDeviceItemSpec(creature[^1], nameHash, MaterialTable.TryGet(nameHash, out var mapMaterials) ? mapMaterials : []);

                if (creature[^1].TryGetComponent<ClientOwnerComponent>(out var mapOwner) && Info.OwnerCharacterId is { } islandOwner)
                    mapOwner.Owner = islandOwner;
            }

            if (creature.Count == 0)
            {
                Console.WriteLine($"[WorldManager] Unknown entity name '{spec.Name}' in WorldMap.json, skipping.");
                continue;
            }

            _mapCreatureSpawns[creature[^1].Id] = (position, spec.Yaw, spec.SpawnParams);

            spawned++;
        }

        Console.WriteLine($"[WorldManager] Spawned {spawned}/{entities.Count} entities from WorldMap.json.");
    }

    private void FillInventory(Entity container, List<string> itemNames)
    {
        if (!container.TryGetComponent<ClientInventoryComponent>(out var inventory))
            return;

        var firstSlot = (int)ClientEquippedItemComponent.SlotCount;
        List<int> items = [.. Enumerable.Repeat(0, firstSlot), .. itemNames
            .Take(Math.Max(0, inventory.MaxInventorySlots - firstSlot))
            .Select(name => PlayerInitializer.CreateInventoryItem(EntityManager, Util.ComputeCrc32(name), 1)?.Id ?? 0)];

        inventory.InventoryEntityList = [.. items, .. Enumerable.Repeat(0, Math.Max(0, inventory.MaxInventorySlots - items.Count))];
    }

    public (Vector3 Position, string? SpawnParams)? MapSpawnOf(int entityId)
        => _mapCreatureSpawns.TryGetValue(entityId, out var spawn) ? (spawn.Position, spawn.SpawnParams) : null;

    public List<Entity> RespawnMapCreature(Entity creature)
    {
        if (!_mapCreatureSpawns.Remove(creature.Id, out var spawn))
            return [];

        var entities = SpawnCreature(creature.Name, spawn.Position);

        if (entities.Count > 0)
        {
            _mapCreatureSpawns[entities[^1].Id] = spawn;

            if (spawn.Yaw is float yaw)
                SetYaw(entities[^1], yaw);

            if (creature.TryGetComponent<CharacterStateComponent>(out var oldState)
                && entities[^1].TryGetComponent<CharacterStateComponent>(out var newState))
                newState.CharacterMentalStateId = oldState.CharacterMentalStateId;
        }

        return entities;
    }

    private static void SetYaw(Entity entity, float yaw)
    {
        if (entity.TryGetComponent<SmoothedTransformComponent>(out var smoothedTransform))
            smoothedTransform.YawDegrees = yaw;
        else if (entity.TryGetComponent<TransformComponent>(out var transform))
            transform.YawDegrees = yaw;
    }

    private static void SetMentalState(Entity creature, string mentalState)
    {
        var state = Array.FindIndex(CharacterStateComponent.MentalStates, name => name.Equals(mentalState, StringComparison.OrdinalIgnoreCase));

        if (state < 0 || !creature.TryGetComponent<CharacterStateComponent>(out var characterState))
        {
            Console.WriteLine($"[WorldManager] {creature.Name}#{creature.Id}: mental state '{mentalState}' not set");
            return;
        }

        characterState.CharacterMentalStateId = (byte)state;
    }

    public List<Entity> SpawnCreature(string name, Vector3 position)
    {
        var loadoutItems = CreatureLoadoutTable.CreateItems(name, EntityManager);

        if (!EntityManager.TryCreateEntity(name, out var entity))
        {
            foreach (var item in loadoutItems)
                if (item.Item is { } itemEntity)
                    EntityManager.RemoveEntity(itemEntity);

            return [];
        }

        if (entity.TryGetComponent<SmoothedTransformComponent>(out var smoothedTransform))
            smoothedTransform.Position = position;
        else if (entity.TryGetComponent<TransformComponent>(out var transform))
            transform.Position = position;

        CreatureLoadoutTable.Equip(entity, loadoutItems);
        Combat.ApplyMaxHealth(entity);

        return [.. loadoutItems.Select(item => item.Item).OfType<Entity>(), entity];
    }

    public void SendInitialChunks(PlayerConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        Console.WriteLine("[SendInitialChunks] Entered.");

        var chunkSyncData = ChunkManager.GetAllChunkSyncData().ToList();

        Console.WriteLine($"[SendInitialChunks] Found {chunkSyncData.Count} chunk(s) to sync.");

        var beginSync = new BeginSync
        {
            NumChunksToSync = chunkSyncData.Count
        };

        Console.WriteLine($"[SendInitialChunks] Sending BeginSync (NumChunksToSync={beginSync.NumChunksToSync})...");

        connection.Send(beginSync);

        Console.WriteLine("[SendInitialChunks] BeginSync sent. Starting chunk loop.");

        foreach (var chunkSync in chunkSyncData)
        {
            var chunkSyncStream = chunkSync.Serialize();
            chunkSyncStream.PrintHex(out var chunkSyncHex);
            Console.WriteLine($"[SendInitialChunks] ChunkSync Coords={chunkSync.Coords} "
                + $"Data1Len={chunkSync.Data1?.Length} Data2Len={chunkSync.Data2?.Length} "
                + $"({chunkSyncStream.GetNumberOfBytesUsed()} bytes, "
                + $"{chunkSyncStream.GetNumberOfBitsUsed()} bits used): "
                + $"{(chunkSyncHex.Length > 200 ? chunkSyncHex[..200] + "..." : chunkSyncHex)}");

            connection.Send(chunkSync);
        }
    }

    public void SendInitialEntities(PlayerConnection connection, Entity player, PlayerInitializer playerInitializer)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(playerInitializer);

        if (connection.CharacterId is not { } characterId)
        {
            Console.WriteLine($"[Auth] {connection.Guid}: no character for this connection (no valid SetUserToken), sending QuitGame.");
            connection.Send(new QuitGame());
            return;
        }

        playerInitializer.InitializePlayer(player, characterId, connection.CharacterName ?? connection.AccountName ?? "");

        if (player.TryGetComponent<SmoothedTransformComponent>(out var transform))
            connection.LookYaw = transform.YawDegrees;

        connection.LookPitch = PlayerSaveStore.Load(characterId)?.LookPitch ?? 0f;

        connection.IsPlayerInitialized = true;

        GameEvents.Record("player.join", Info.Id, characterId, new { kind = Info.Kind.ToString(), template = Info.Template });

        foreach (var entity in InventoryFirst(EntityManager.Entities))
        {
            var entityAdd = new EntityAdd
            {
                Id = entity.Id,
                NameHash = Util.ComputeCrc32(entity.Name),
                SyncData = entity.GetSyncData(newEntity: true, includeOwnerOnly: entity == player)
            };

            var entityAddStream = entityAdd.Serialize();
            entityAddStream.PrintHex(out var entityAddHex);
            Console.WriteLine($"[SendInitialEntities] EntityAdd Id={entityAdd.Id} Name='{entity.Name}' "
                + $"NameHash={entityAdd.NameHash} SyncDataBits={entityAdd.SyncData.GetNumberOfBitsUsed()} "
                + $"({entityAddStream.GetNumberOfBytesUsed()} bytes, "
                + $"{entityAddStream.GetNumberOfBitsUsed()} bits used): "
                + $"{(entityAddHex.Length > 200 ? entityAddHex[..200] + "..." : entityAddHex)}");

            connection.Send(entityAdd);
        }

        connection.Send(new ClientEntitiesSyncFinished());
    }

    private IEnumerable<Entity> InventoryFirst(IEnumerable<Entity> entities)
    {
        var sent = new HashSet<int>();
        var ordered = new List<Entity>();

        void Visit(Entity entity)
        {
            if (!sent.Add(entity.Id))
                return;

            InventoryComponent? inventory = entity.TryGetComponent<ClientInventoryComponent>(out var clientInventory)
                ? clientInventory
                : entity.TryGetComponent<InventoryComponent>(out var serverInventory) ? serverInventory : null;

            if (inventory is not null)
                foreach (var id in inventory.InventoryEntityList.Append(inventory.VaultInventoryEntity))
                    if (id != 0 && EntityManager.TryGetEntity(id, out var item))
                        Visit(item);

            ordered.Add(entity);
        }

        foreach (var entity in entities)
            Visit(entity);

        return ordered;
    }

    public void Reset()
    {
        ((EntityManager)EntityManager).Clear();
        _mapCreatureSpawns.Clear();
        ChunkManager.ClearAll();
        InitializeWorld();
    }
}
