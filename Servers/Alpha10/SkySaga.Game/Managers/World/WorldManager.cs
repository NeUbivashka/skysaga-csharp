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

    private readonly Dictionary<int, (Vector3 Position, float? Yaw, string? SpawnParams)> _mapCreatureSpawns = [];
    private readonly Dictionary<int, List<TreePartPlacement>> _generatedTreeParts = [];

    private const int LandingSearchRadius = 16;

    private const byte TreeVoxel = 19;
    private const byte BarkBrownVoxel = 13;
    private const byte BarkPurpleVoxel = 109;
    private const uint ForestTreeType = 0x1ECED691;
    private const uint JungleParmaVioletPalette = 34;

    // TestZoo and the per-player home maps copied from it contain a flat,
    // unused forest area where the generated grove does not cover map content.
    private static readonly Vector3Int[] DefaultTreeGrove =
    [
        new(92, 32, 92),
        new(108, 32, 88),
        new(124, 32, 94),
        new(140, 32, 90),
        new(88, 32, 112),
        new(104, 32, 110),
        new(122, 32, 114),
        new(142, 32, 108),
        new(94, 32, 132),
        new(112, 32, 128),
        new(130, 32, 134),
        new(146, 32, 126),
    ];

    // Small authored groves for the recreated Forest and Jungle mastery maps;
    // these are not a reconstruction of the original Alpha 10 layouts.
    private static readonly Vector3Int[] AdventureTreeGrove =
    [
        new(72, 32, 72), new(94, 32, 86), new(118, 32, 70),
        new(148, 32, 100), new(88, 32, 122), new(126, 32, 132),
        new(168, 32, 142), new(202, 32, 118), new(224, 32, 78),
        new(188, 32, 62), new(250, 32, 96), new(274, 32, 140),
    ];

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
            AddDefaultTrees();
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
        ReconcileGeneratedTreeParts();
        LoadDevices();

        foreach (var entity in EntityManager.Entities.ToList())
            PlaceLinkedVoxels(entity);

        SpawnTimeOfDay();
        SpawnWorldComposition();
        SpawnAdventureName();
    }

    private void AddDefaultTrees()
    {
        if (Environment.GetEnvironmentVariable("SKYSAGA_WORLD") == "social")
            return;

        var mapName = Path.GetFileNameWithoutExtension(WorldMapLoader.MapFileName);
        var adventureGrove = mapName.Equals("AdventureForest01", StringComparison.OrdinalIgnoreCase)
            || mapName.Equals("AdventureJungle01", StringComparison.OrdinalIgnoreCase);

        if (!adventureGrove
            && (!mapName.Equals(Path.GetFileNameWithoutExtension(WorldMapLoader.DefaultMap), StringComparison.OrdinalIgnoreCase)
                && !mapName.StartsWith("home_", StringComparison.OrdinalIgnoreCase)))
            return;

        int placed = 0;

        var grove = adventureGrove ? AdventureTreeGrove : DefaultTreeGrove;
        for (int treeIndex = 0; treeIndex < grove.Length; treeIndex++)
        {
            var root = grove[treeIndex];

            if (GetWorldVoxel(root) != WorldChunk.Air
                || GetWorldVoxel(root + new Vector3Int(0, -1, 0)) == WorldChunk.Air)
                continue;

            bool hasClearance = true;
            for (int y = 0; y <= 17 && hasClearance; y++)
                for (int z = -4; z <= 4 && hasClearance; z++)
                    for (int x = -4; x <= 4; x++)
                        if (GetWorldVoxel(root + new Vector3Int(x, y, z)) != WorldChunk.Air)
                        {
                            hasClearance = false;
                            break;
                        }

            if (!hasClearance)
                continue;

            if (PlaceProceduralTree(root, treeIndex))
                placed++;
        }

        Console.WriteLine($"[WorldManager] Added {placed} procedural tree(s) to the default forest grove.");
    }

    private readonly record struct TreePartPlacement(TreePart Part, Vector3Int Block);

    private bool PlaceProceduralTree(Vector3Int root, int treeIndex)
    {
        List<TreePartPlacement> placements = [];
        bool parmaViolet = Definition.Spec.Palette == JungleParmaVioletPalette;

        int AddPart(byte partType, byte direction, int parentIndex, bool decoration = false)
        {
            var block = parentIndex < 0
                ? root
                : placements[parentIndex].Block + TreeDirectionOffset(direction);

            placements.Add(new TreePartPlacement(
                new TreePart(partType, direction, parentIndex, 0, decoration, TreeVoxel),
                block));

            return placements.Count - 1;
        }

        int trunkParent = AddPart(0, 0, -1);
        int trunkHeight = 10 + treeIndex % 3;

        for (int level = 1; level <= trunkHeight; level++)
        {
            bool branchTier = level >= 4 && level <= trunkHeight - 2 && (level - 4) % 3 == 0;
            int trunk = AddPart(branchTier ? (byte)17 : (byte)2, 4, trunkParent);

            if (branchTier)
            {
                for (byte direction = 0; direction < 4; direction++)
                {
                    int branchParent = trunk;

                    // The lowest tier extends two blocks; higher tiers use a
                    // one-block tip so the crown narrows towards the top.
                    if (level == 4)
                        branchParent = AddPart((byte)(19 + direction), direction, branchParent);

                    int branchEnd = AddPart((byte)(25 + direction), direction, branchParent);
                    AddPart((byte)(50 + direction), 6, branchEnd, decoration: true);
                }
            }
            else if (level >= 3)
            {
                AddPart((byte)(((level + treeIndex) & 1) == 0 ? 43 : 49), 6, trunk, decoration: true);
            }

            trunkParent = trunk;
        }

        int top = AddPart(18, 4, trunkParent);
        AddPart(37, 4, top, decoration: true);

        if (placements.Count >= 0x7F || !EntityManager.TryCreateEntity("Tree", out var tree))
            return false;

        if (!tree.TryGetComponent<ClientTreeComponent>(out var treeComponent)
            || !tree.TryGetComponent<TransformComponent>(out var transform)
            || !tree.TryGetComponent<MaterialCompositionComponent>(out var composition))
        {
            EntityManager.RemoveEntity(tree);
            return false;
        }

        foreach (var block in placements.Select(placement => placement.Block).Distinct())
            SetWorldVoxelRaw(block, TreeVoxel);

        treeComponent.Description = [.. placements.Select(placement => placement.Part)];
        treeComponent.PartsDestroyed = [.. Enumerable.Repeat(false, placements.Count)];
        treeComponent.TreeType = ForestTreeType;

        composition.MaterialList =
        [
            Util.ComputeCrc32(parmaViolet ? "Wood_Purple" : "Wood_Brown"),
            Util.ComputeCrc32(parmaViolet ? "Plant_Pink" : "Plant_Green")
        ];

        // Tree roots are rounded with floor(position + 0.5) by the client.
        // Integer coordinates therefore identify the intended root block.
        transform.Position = new Vector3(root.X, root.Y, root.Z);
        _generatedTreeParts[tree.Id] = placements;

        return true;
    }

    public bool MarkTreePartDestroyed(int entityId, Vector3Int worldVoxel, out byte originalVoxelType)
    {
        originalVoxelType = WorldChunk.Air;

        if (!_generatedTreeParts.TryGetValue(entityId, out var placements)
            || !EntityManager.TryGetEntity(entityId, out var tree)
            || !tree.TryGetComponent<ClientTreeComponent>(out var treeComponent))
            return false;

        List<bool> destroyed = [.. treeComponent.PartsDestroyed];
        bool changed = false;

        for (int index = 0; index < placements.Count; index++)
        {
            if (placements[index].Block != worldVoxel || destroyed[index])
                continue;

            destroyed[index] = true;
            changed = true;

            // Voxel 19 is required in the tree description for the client to
            // construct the model, but it has no pickup mapping. Resolve its
            // harvested log material here without altering the description.
            originalVoxelType = Definition.Spec.Palette == JungleParmaVioletPalette
                ? BarkPurpleVoxel
                : BarkBrownVoxel;
        }

        if (!changed)
            return false;

        treeComponent.PartsDestroyed = destroyed;
        Console.WriteLine($"[Tree] entity {entityId} destroyed part(s) at {worldVoxel}");
        return true;
    }

    private void ReconcileGeneratedTreeParts()
    {
        foreach (var (entityId, placements) in _generatedTreeParts)
        {
            if (!EntityManager.TryGetEntity(entityId, out var tree)
                || !tree.TryGetComponent<ClientTreeComponent>(out var treeComponent))
                continue;

            List<bool> destroyed = [.. treeComponent.PartsDestroyed];
            bool changed = false;

            for (int index = 0; index < placements.Count; index++)
            {
                if (GetWorldVoxel(placements[index].Block) != WorldChunk.Air || destroyed[index])
                    continue;

                destroyed[index] = true;
                changed = true;
            }

            if (changed)
                treeComponent.PartsDestroyed = destroyed;
        }
    }

    private static Vector3Int TreeDirectionOffset(byte direction) => direction switch
    {
        0 => new Vector3Int(1, 0, 0),
        1 => new Vector3Int(-1, 0, 0),
        2 => new Vector3Int(0, 0, 1),
        3 => new Vector3Int(0, 0, -1),
        4 or 7 => new Vector3Int(0, 1, 0),
        5 => new Vector3Int(0, -1, 0),
        6 => new Vector3Int(0, 0, 0),
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
    };

    private byte GetWorldVoxel(Vector3Int world)
    {
        var chunk = new Vector3Int(
            FloorDiv(world.X, ChunkSize),
            FloorDiv(world.Y, ChunkSize),
            FloorDiv(world.Z, ChunkSize));
        var local = new Vector3Int(
            world.X - chunk.X * ChunkSize,
            world.Y - chunk.Y * ChunkSize,
            world.Z - chunk.Z * ChunkSize);

        return ChunkManager.GetVoxel(chunk, local);
    }

    private void SetWorldVoxelRaw(Vector3Int world, byte voxelType)
    {
        var chunk = new Vector3Int(
            FloorDiv(world.X, ChunkSize),
            FloorDiv(world.Y, ChunkSize),
            FloorDiv(world.Z, ChunkSize));
        var local = new Vector3Int(
            world.X - chunk.X * ChunkSize,
            world.Y - chunk.Y * ChunkSize,
            world.Z - chunk.Z * ChunkSize);

        ChunkManager.GetOrCreateChunk(chunk).SetVoxel(local, voxelType);
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

        // Airship_Arrive_CoFL selects its first City prop by this tag. Its
        // second prop is HUB_NPC_RecipeBuyerShop, supplied by the City map
        // entity conversion below. Both are required for the client sequence.
        if (Environment.GetEnvironmentVariable("SKYSAGA_WORLD") == "social" &&
            airShip.TryGetComponent<TagsComponent>(out var tags))
            tags.TagList = [Util.ComputeCrc32("AnimSequenceEntity")];

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

    private void SpawnAdventureName()
    {
        // The client creates the adventure title from this entity. Normal player
        // islands and the social hub do not send it, so leave their title flow alone.
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SKYSAGA_ADVENTURE")))
        {
            return;
        }

        if (!EntityManager.TryCreateEntity("AdventureName", out var adventureName) ||
            !adventureName.TryGetComponent<ClientAdventureNameComponent>(out var component))
        {
            Console.WriteLine("[WorldManager] Could not create the AdventureName entity.");
            return;
        }

        component.RandomSeed = Definition.Spec.Seed;
        // Mastery maps currently do not specify a primary creature set. The client
        // represents that as table index zero (not a literal CRC value).
        component.PrimaryFeatureCreatureSetCrc = 0;
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

        // ReturnTeleporter is a solid 9x9 plane of voxel 39. That material is
        // not rendered by this reconstructed map, while replacing the whole
        // plane with opaque voxel 38 creates a large wall. Keep the original
        // interaction entity and render only its outer frame with the known
        // visible fallback material.
        if (Environment.GetEnvironmentVariable("SKYSAGA_WORLD") == "social"
            && entity.Name.Equals("ReturnTeleporter", StringComparison.OrdinalIgnoreCase))
        {
            voxelLink.Voxels = [.. voxelLink.Voxels
                .Where(voxel => voxel.VoxelType != 39 ||
                    (voxel.Offset.Z == 0 &&
                        (voxel.Offset.Y == 0 || voxel.Offset.Y == 8 || Math.Abs(voxel.Offset.X) == 4)))
                .Select(voxel => voxel.VoxelType == 39 ? voxel with { VoxelType = 38 } : voxel)];

            Console.WriteLine($"[WorldManager] Rendered the City portal as a {voxelLink.Voxels.Count}-voxel frame.");
        }

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
        ConfigureWritableStorage(entity);

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
        var ownerUuid = Environment.GetEnvironmentVariable("SKYSAGA_HOME_OWNER_UUID") ?? Util.CharacterUuid();
        var isSocialWorld = Environment.GetEnvironmentVariable("SKYSAGA_WORLD") == "social";

        if (Environment.GetEnvironmentVariable("SKYSAGA_CRAFTING_TEST_ROW") == "1")
        {
            entities = [.. entities, .. CraftingStationTestRow()];
            Console.WriteLine("[WorldManager] Added the Alpha 10 crafting-station test row.");
        }

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

            // FTUECOFL_ARRIVE_AIRSHIP names HUB_NPC_RecipeBuyerShop as its
            // dialogue speaker and arrival prop. The prototype previously
            // placed a generic trader here, leaving the cutscene without its
            // second required prop. It occupies the same City-only position.
            var entityName = isSocialWorld &&
                spec.Name.Equals("HUB_NPC_Trader", StringComparison.OrdinalIgnoreCase)
                ? "HUB_NPC_RecipeBuyerShop"
                : spec.Name;

            var creature = SpawnCreature(entityName, position);

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

                if (creature[^1].TryGetComponent<ClientOwnerComponent>(out var mapOwner) && Guid.TryParse(ownerUuid, out var islandOwner))
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

        AddAdventureSuppliesChests();

        Console.WriteLine($"[WorldManager] Spawned {spawned}/{entities.Count} entities from WorldMap.json.");
    }

    private void AddAdventureSuppliesChests()
    {
        // These are opt-in test supplies for the three Mastery cards.  They
        // are generated at startup rather than written into a player's island
        // save, so they do not alter an existing chest or player inventory.
        if (Environment.GetEnvironmentVariable("SKYSAGA_ADVENTURE_SUPPLIES_CHESTS") != "1")
            return;

        var supplies = new[]
        {
            (Biome: "Forest", Position: new Vector3(90.5f, 32f, 22.5f)),
            (Biome: "Desert", Position: new Vector3(94.5f, 32f, 22.5f)),
            (Biome: "Jungle", Position: new Vector3(98.5f, 32f, 22.5f)),
        };

        var chests = 0;
        foreach (var supply in supplies)
        {
            var spawned = SpawnCreature("Chest_Generic_Major", supply.Position);
            if (spawned.Count == 0)
                continue;

            FillAdventureSupplies(spawned[^1], supply.Biome);
            chests++;
        }

        Console.WriteLine($"[WorldManager] Added {chests} Mastery adventure-supplies chest(s) near the home spawn.");
    }

    private void FillAdventureSupplies(Entity chest, string biome)
    {
        if (!chest.TryGetComponent<ClientInventoryComponent>(out var inventory))
            return;

        const int fragmentCount = 25;
        const int travelObjectCount = 10;
        var firstSlot = (int)ClientEquippedItemComponent.SlotCount;
        var fragmentMaterials = new uint?[] { Util.ComputeCrc32(biome) };

        var itemIds = new List<int>
        {
            PlayerInitializer.CreateInventoryItem(EntityManager, Util.ComputeCrc32("Keystone_Fragment"), fragmentCount,
                materialOverrides: fragmentMaterials)?.Id ?? 0,
            PlayerInitializer.CreateInventoryItem(EntityManager, Util.ComputeCrc32("Realm_Travelling_Object"), travelObjectCount)?.Id ?? 0,
        };

        var slots = new List<int>(Enumerable.Repeat(0, firstSlot));
        slots.AddRange(itemIds.Take(Math.Max(0, inventory.MaxInventorySlots - slots.Count)));
        slots.AddRange(Enumerable.Repeat(0, Math.Max(0, inventory.MaxInventorySlots - slots.Count)));
        inventory.InventoryEntityList = slots;
    }

    private static IEnumerable<WorldMapEntityData> CraftingStationTestRow()
    {
        // Alpha 10 only has complete item and device definitions for these
        // three entries. The old station names are client templates without a
        // placeable item/device definition, so the client renders its generic
        // device-placeholder model for them.
        string[] stations = ["Anvil_2x1x1", "Crafting_Device", "Fireworks_Device"];

        // TestZoo has a flat floor at y=28 and its player marker is at x=96,
        // z=22. Place the row on the same island, five voxels apart, at z=34.
        for (var index = 0; index < stations.Length; index++)
            yield return new WorldMapEntityData
            {
                Name = stations[index],
                X = 48 + index * 5,
                Y = 32,
                Z = 34,
                Yaw = 180
            };
    }

    private static void ConfigureWritableStorage(Entity entity)
    {
        if (!entity.TryGetComponent<ClientPerUserInventoryStoreComponent>(out var store))
            return;

        // Adventure, end-game, keyed and PvP reward chests deliberately expose
        // a take-only loot flow. Regular owner chests use this same component,
        // but advertise islootchest=false and are writable storage.
        if (entity.BoolParameter("islootchest") == true)
            return;

        store.IsLootFountain = false;

        if (entity.TryGetComponent<ClientInventoryComponent>(out var inventory))
        {
            inventory.TakeOnly = false;

            if (inventory.InventoryEntityList.Count < inventory.MaxInventorySlots)
            {
                List<int> slots = [.. inventory.InventoryEntityList];
                slots.AddRange(Enumerable.Repeat(0, inventory.MaxInventorySlots - slots.Count));
                inventory.InventoryEntityList = slots;
            }
        }
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

        ConfigureWritableStorage(entity);
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

        playerInitializer.InitializePlayer(player, connection);

        if (player.TryGetComponent<SmoothedTransformComponent>(out var transform))
            connection.LookYaw = transform.YawDegrees;

        connection.LookPitch = connection.SaveFileName is { } saveFileName ? PlayerSaveStore.Load(saveFileName)?.LookPitch ?? 0f : 0f;

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

        // ClientSettingsComponent camera state is owner-only. Send the saved
        // value after the owner's Player entity exists, without marking the
        // entire entity as local early in the join sequence.
        connection.Send(new EntitySync
        {
            Id = player.Id,
            SyncData = player.GetSyncData(
                newEntity: false,
                forOwner: true,
                onlyParameter: "CurrentGameplayCameraState")
        });

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
        _generatedTreeParts.Clear();
        ChunkManager.ClearAll();
        InitializeWorld();
    }
}
