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

    public IMapEntityManager EntityManager { get; }
    public IMapChunkManager ChunkManager => _chunkManager;
    public MapDefinition Definition { get; set; }
    public Entity? Airship { get; private set; }

    private static readonly string DevicesPath = Path.Combine(AppContext.BaseDirectory, "Data", "Saves",
        $"devices_{Path.GetFileNameWithoutExtension(WorldMapLoader.MapFileName)}.json");

    public record StoredItem(uint ItemCrc, int Count, uint?[] Materials);

    public record PlacedDevice(uint ItemCrc, int X, int Y, int Z, Guid? Owner, uint?[] Materials)
    {
        public List<StoredItem?> Items { get; set; } = [];
    }

    private readonly Dictionary<int, PlacedDevice> _deviceEntities = [];

    private readonly List<PlacedDevice> _devices = [];

    private static readonly string CheckpointsPath = Path.Combine(AppContext.BaseDirectory, "Data", "Saves",
        $"checkpoints_{Path.GetFileNameWithoutExtension(WorldMapLoader.MapFileName)}.json");

    private const int CheckpointSearchRadius = 4;
    private const int CheckpointClearance = 2;

    private Dictionary<Guid, float[]> _checkpoints = LoadCheckpoints();
    private readonly object _devicesLock = new();

    private readonly Dictionary<int, Vector3> _mapCreatureSpawns = [];

    private const int LandingSearchRadius = 16;

    public WorldManager(PlayerConnectionManager playerConnectionManager)
    {
        ArgumentNullException.ThrowIfNull(playerConnectionManager);

        EntityManager = new EntityManager();
        _chunkManager = new MapChunkManager(playerConnectionManager);

        InitializeWorld();
        SetupMapFileWatcher();
    }

    private void SetupMapFileWatcher()
    {
        try
        {
            string fullPath = Path.GetFullPath(WorldMapLoader.MapPath);
            string? dir = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(dir))
                return;

            Directory.CreateDirectory(dir);

            _mapFileWatcher = new FileSystemWatcher(dir)
            {
                Filter = WorldMapLoader.MapFileName + "*",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            _mapFileWatcher.Changed += OnMapFileChanged;
            _mapFileWatcher.Created += OnMapFileChanged;
            _mapFileWatcher.Renamed += OnMapFileChanged;

            Console.WriteLine($"[WorldManager] Watching '{dir}' for WorldMap.json changes - editing the map and saving will auto-reload it without restarting the server.");
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
                Console.WriteLine("[WorldManager] WorldMap.json changed on disk - reloading...");
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

        if (WorldMapLoader.TryLoad(ChunkManager, out var loadedDefinition, out var loadedEntities, WorldMapLoader.MapPath) && loadedDefinition is not null)
        {
            Definition = loadedDefinition;

            SpawnEntitiesFromMap(loadedEntities, out var mapAirshipPosition);

            SpawnAirship(mapAirshipPosition);
        }
        else
        {
            Console.WriteLine(
                $"[WorldManager] No map found at '{WorldMapLoader.MapPath}' - starting an empty world. " +
                "Export a map from the terrain editor and place it there.");

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

    public List<Entity> PlaceDevice(uint itemCrc, DeviceInfo device, Vector3Int voxel, Guid? owner, uint?[] materials)
    {
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

            if (!File.Exists(DevicesPath))
                return;

            try
            {
                _devices.AddRange(System.Text.Json.JsonSerializer.Deserialize<List<PlacedDevice>>(File.ReadAllText(DevicesPath)) ?? []);
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or IOException)
            {
                Console.WriteLine($"[WorldManager] Failed to read '{DevicesPath}': {ex.Message}");
                return;
            }

            foreach (var placed in _devices)
                if (DeviceTable.TryGet(placed.ItemCrc, out var device))
                    SpawnDevice(placed, device);

            Console.WriteLine($"[WorldManager] Loaded {_devices.Count} placed devices.");
        }
    }

    private void SaveDevices()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DevicesPath)!);
            File.WriteAllText(DevicesPath, System.Text.Json.JsonSerializer.Serialize(_devices));
        }
        catch (IOException ex)
        {
            Console.WriteLine($"[WorldManager] Failed to save '{DevicesPath}': {ex.Message}");
        }
    }

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
        {
            _checkpoints[character] = [transform.Position.X, transform.Position.Y, transform.Position.Z];

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CheckpointsPath)!);
                File.WriteAllText(CheckpointsPath, System.Text.Json.JsonSerializer.Serialize(_checkpoints));
            }
            catch (IOException ex)
            {
                Console.WriteLine($"[WorldManager] Failed to save '{CheckpointsPath}': {ex.Message}");
            }
        }

        Console.WriteLine($"[Checkpoint] {character} -> {checkpoint.Name}#{checkpoint.Id} at {transform.Position}");
    }

    private static Dictionary<Guid, float[]> LoadCheckpoints()
    {
        try
        {
            if (File.Exists(CheckpointsPath))
                return System.Text.Json.JsonSerializer.Deserialize<Dictionary<Guid, float[]>>(File.ReadAllText(CheckpointsPath)) ?? [];
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or IOException)
        {
            Console.WriteLine($"[WorldManager] Failed to read '{CheckpointsPath}': {ex.Message}");
        }

        return [];
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

            if (creature.Count > 0 && DeviceTable.TryGetByEntity(spec.Name, out _))
            {
                var nameHash = Util.ComputeCrc32(spec.Name);
                SetDeviceItemSpec(creature[^1], nameHash, MaterialTable.TryGet(nameHash, out var mapMaterials) ? mapMaterials : []);

                if (creature[^1].TryGetComponent<ClientOwnerComponent>(out var mapOwner) && Guid.TryParse(Util.CharacterUuid(), out var islandOwner))
                    mapOwner.Owner = islandOwner;
            }

            if (creature.Count == 0)
            {
                Console.WriteLine($"[WorldManager] Unknown entity name '{spec.Name}' in WorldMap.json, skipping.");
                continue;
            }

            _mapCreatureSpawns[creature[^1].Id] = position;

            spawned++;
        }

        Console.WriteLine($"[WorldManager] Spawned {spawned}/{entities.Count} entities from WorldMap.json.");
    }

    private void FillInventory(Entity container, List<string> itemNames)
    {
        if (!container.TryGetComponent<ClientInventoryComponent>(out var inventory))
            return;

        var items = itemNames
            .Select(name => PlayerInitializer.CreateInventoryItem(EntityManager, Util.ComputeCrc32(name), 1))
            .Select(item => item?.Id ?? 0)
            .Take(inventory.MaxInventorySlots)
            .ToList();

        inventory.InventoryEntityList = [.. items, .. Enumerable.Repeat(0, Math.Max(0, inventory.MaxInventorySlots - items.Count))];
    }

    public List<Entity> RespawnMapCreature(Entity creature)
    {
        if (!_mapCreatureSpawns.Remove(creature.Id, out var position))
            return [];

        var entities = SpawnCreature(creature.Name, position);

        if (entities.Count > 0)
            _mapCreatureSpawns[entities[^1].Id] = position;

        return entities;
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

        playerInitializer.InitializePlayer(player);

        connection.IsPlayerInitialized = true;

        foreach (var entity in InventoryFirst(EntityManager.Entities))
        {
            var entityAdd = new EntityAdd
            {
                Id = entity.Id,
                NameHash = Util.ComputeCrc32(entity.Name),
                SyncData = entity.GetSyncData(newEntity: true)
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
