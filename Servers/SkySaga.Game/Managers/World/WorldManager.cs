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
    public Vector3 AirshipPosition { get; private set; }
    public Vector3? PlayerSpawnPosition { get; private set; }

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
            string fullPath = Path.GetFullPath(WorldMapLoader.DefaultPath);
            string? dir = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(dir))
                return;

            Directory.CreateDirectory(dir);

            _mapFileWatcher = new FileSystemWatcher(dir)
            {
                Filter = "WorldMap.json*",
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

            SpawnEntitiesFromMap(meandrosEntities, out var meandrosAirshipPosition, out var meandrosPlayerSpawnPosition);

            SpawnAirship(meandrosAirshipPosition);
            PlayerSpawnPosition = meandrosPlayerSpawnPosition;
            ChunkManager.LoadSavedEdits();

            return;
        }

        var mapPath = Environment.GetEnvironmentVariable("SKYSAGA_MAP") is { Length: > 0 } map
            ? Path.Combine("Data", map)
            : WorldMapLoader.DefaultPath;

        if (WorldMapLoader.TryLoad(ChunkManager, out var loadedDefinition, out var loadedEntities, mapPath) && loadedDefinition is not null)
        {
            Definition = loadedDefinition;

            SpawnEntitiesFromMap(loadedEntities, out var mapAirshipPosition, out var mapPlayerSpawnPosition);

            SpawnAirship(mapAirshipPosition);
            PlayerSpawnPosition = mapPlayerSpawnPosition;
        }
        else
        {
            Console.WriteLine(
                $"[WorldManager] No map found at '{WorldMapLoader.DefaultPath}' - starting an empty world. " +
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
            PlayerSpawnPosition = null;
        }

        ChunkManager.LoadSavedEdits();
        PlayerSpawnPosition ??= SpawnFinder.FindSpawnNearPoint(ChunkManager, AirshipPosition, 5, 10, Definition.MapSizeChunks, _random);
        SpawnTimeOfDay();
        SpawnWorldComposition();
    }

    private void SpawnAirship(Vector3? mapPosition)
    {
        var position = mapPosition
            ?? SpawnFinder.FindEdgeSpawnPoint(ChunkManager, Definition.MapSizeChunks, _random)
            ?? SpawnFinder.FindSafeSpawnPoint(ChunkManager, Definition.MapSizeChunks);

        AirshipPosition = position;

        if (!EntityManager.TryCreateEntity("Airship", out var airShip))
            return;

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

    private void SpawnTimeOfDay()
    {
        if (EntityManager.TryCreateEntity("TimeOfDay", out var timeOfDay) &&
            timeOfDay.TryGetComponent<ClientTimeOfDayComponent>(out var clientTimeOfDayComponent))
        {
            clientTimeOfDayComponent.StartTimeOfDay =
                int.TryParse(Environment.GetEnvironmentVariable("SKYSAGA_TIME_OF_DAY"), out var tod) ? tod : 32768;
            clientTimeOfDayComponent.FixedTimeOfDay = true;
            clientTimeOfDayComponent.DayNightCycleDuration = 20;
            clientTimeOfDayComponent.RealWorldStartTime = RakNet.RakNet.GetTime();
            clientTimeOfDayComponent.TimeStretch = 8;

            clientTimeOfDayComponent.TimeOfDayOffset = 0;
        }
    }

    private void SpawnEntitiesFromMap(List<WorldMapEntityData> entities, out Vector3? airshipPosition, out Vector3? playerSpawnPosition)
    {
        airshipPosition = null;
        playerSpawnPosition = null;

        int spawned = 0;

        foreach (var spec in entities)
        {
            if (string.IsNullOrWhiteSpace(spec.Name))
                continue;

            var position = new Vector3(spec.X + 0.5f, spec.Y, spec.Z + 0.5f);

            if (string.Equals(spec.Name, "Airship", StringComparison.OrdinalIgnoreCase))
            {
                airshipPosition = position;
                continue;
            }

            if (string.Equals(spec.Name, "TimeOfDay", StringComparison.OrdinalIgnoreCase))
                continue;

            if (string.Equals(spec.Name, "Player", StringComparison.OrdinalIgnoreCase))
            {
                playerSpawnPosition = position;
                continue;
            }

            if (SpawnCreature(spec.Name, position).Count == 0)
            {
                Console.WriteLine($"[WorldManager] Unknown entity name '{spec.Name}' in WorldMap.json, skipping.");
                continue;
            }

            spawned++;
        }

        Console.WriteLine($"[WorldManager] Spawned {spawned}/{entities.Count} entities from WorldMap.json.");
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

        foreach (var entity in EntityManager.Entities)
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

    public void Reset()
    {
        ((EntityManager)EntityManager).Clear();
        ChunkManager.ClearAll();
        InitializeWorld();
    }
}
