using SkySaga.Data.Entities;

namespace SkySaga.Data;

public static class InitialCaveDisplay
{
    private const byte StoneBlock = 2;
    private const byte GlowingMushroomBlock = 136;
    private const byte GlowingCrystalBlock = 137;
    private const byte OnFloor = 2;
    private const byte OnCeiling = 3;
    private const int DisplayWidth = 15;
    private const int DisplayDepth = 7;
    private const int DisplayHeight = 5;
    private const int MinRadius = 7;
    private const int MaxRadius = 32;
    private const int MaxCrystalDistance = 4;
    private const int ArrivalClearance = 6;

    private static readonly string[] Jewels = ["Jewel_Green", "Jewel_Orange", "Jewel_Purple", "Jewel_White", "Jewel_Blue"];

    private static readonly string[] LargeCrystalJewels = ["Jewel_White", "Jewel_Green", "Jewel_Orange", "Jewel_Purple", "Jewel_Blue"];

    private static readonly (string Item, int Height, string[] Materials)[] Hanging =
    [
        ("Stalactite_Small", 1, ["Stone_Blue"]),
        ("Stalactite_Medium", 2, ["Stone_Blue"]),
        ("Stalactite_Large", 3, ["Stone_Blue"]),
        ("Stalactite_Ice_Small", 1, []),
        ("Stalactite_Ice_Medium", 2, []),
        ("Stalactite_Ice_Large", 3, []),
        ("Cave_Crystal_Hanging_A", 2, ["Jewel_White", "Stone_Blue"])
    ];

    internal static void Add(SkySagaDbContext db, World island, InitialOreVeins.IslandSite site, List<(int X, int Y, int Z)> veins, int veinLength)
    {
        var veinCrystal = InitialOreVeins.NameCrc("Cave_Crystal_Clump_A_Lit");
        var crystals = db.PlacedDevices.Any(device => device.WorldId == island.Id && device.OwnerCharacterId == null && device.ItemCrc == veinCrystal)
            ? []
            : PlaceVeinCrystals(db, island, site.Voxels, site.Entities, veins, site.Size, veinLength);

        if (!PlaceDisplay(db, island, site.Voxels, site.Entities, veins, crystals, site.Arrival, site.Size))
            Console.WriteLine($"[WorldCatalog] No safe space found for the cave display on island {island.Id}.");

        island.CaveDisplayPlaced = true;
    }

    public static void AddToExisting(SkySagaDbContext db, World island, byte[] template, int veinLength)
    {
        if (InitialOreVeins.ReadSite(db, island, template) is not { } site)
            return;

        var veins = db.OreVeins.Where(vein => vein.WorldId == island.Id)
            .OrderBy(vein => vein.Kind).ThenBy(vein => vein.Z).ThenBy(vein => vein.X)
            .AsEnumerable()
            .Select(vein => (vein.X, vein.Y, vein.Z))
            .ToList();

        Add(db, island, site, veins, veinLength);
    }

    private static List<(int X, int Y, int Z)> PlaceVeinCrystals(SkySagaDbContext db, World island, HashSet<(int X, int Y, int Z)> voxels,
        List<(int X, int Y, int Z)> entities, List<(int X, int Y, int Z)> veins, (int X, int Y, int Z) size, int veinLength)
    {
        var crystals = new List<(int X, int Y, int Z)>();

        foreach (var vein in veins.Take(4 * veinLength).Chunk(veinLength))
            if (FindCrystalSpot(voxels, entities, veins, crystals, vein[vein.Length / 2], size) is { } spot)
            {
                AddDevice(db, island, "Cave_Crystal_Clump_A_Lit", spot, OnFloor, Jewels[crystals.Count]);
                crystals.Add(spot);
            }

        return crystals;
    }

    private static (int X, int Y, int Z)? FindCrystalSpot(HashSet<(int X, int Y, int Z)> voxels, List<(int X, int Y, int Z)> entities,
        List<(int X, int Y, int Z)> veins, List<(int X, int Y, int Z)> crystals, (int X, int Y, int Z) origin, (int X, int Y, int Z) size)
    {
        for (var radius = 1; radius <= MaxCrystalDistance; radius++)
        for (var dz = -radius; dz <= radius; dz++)
        for (var dx = -radius; dx <= radius; dx++)
        {
            if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != radius)
                continue;

            var spot = (X: origin.X + dx, origin.Y, Z: origin.Z + dz);

            if (spot.X >= 1 && spot.X < size.X && spot.Z >= 1 && spot.Z < size.Z
                && !voxels.Contains(spot) && !voxels.Contains((spot.X, spot.Y + 1, spot.Z)) && voxels.Contains((spot.X, spot.Y - 1, spot.Z))
                && !NearEntity(entities, spot.X, spot.Y, spot.Z) && !veins.Contains(spot) && !crystals.Contains(spot))
                return spot;
        }

        return null;
    }

    private static bool PlaceDisplay(SkySagaDbContext db, World island, HashSet<(int X, int Y, int Z)> voxels, List<(int X, int Y, int Z)> entities,
        List<(int X, int Y, int Z)> veins, List<(int X, int Y, int Z)> crystals, (int X, int Y, int Z) arrival, (int X, int Y, int Z) size)
    {
        for (var radius = MinRadius; radius <= MaxRadius; radius++)
        for (var dz = -radius; dz <= radius; dz++)
        for (var dx = -radius; dx <= radius; dx++)
        {
            if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != radius)
                continue;

            var corner = (X: arrival.X + dx, arrival.Y, Z: arrival.Z + dz);

            if (Fits(voxels, entities, veins, crystals, corner, arrival, size))
            {
                PlaceAt(db, island, corner.X, corner.Y, corner.Z);
                Console.WriteLine($"[WorldCatalog] Island {island.Id}: cave display at ({corner.X}, {corner.Y}, {corner.Z}), {DisplayWidth} x {DisplayDepth}.");
                return true;
            }
        }

        return false;
    }

    private static bool Fits(HashSet<(int X, int Y, int Z)> voxels, List<(int X, int Y, int Z)> entities, List<(int X, int Y, int Z)> veins,
        List<(int X, int Y, int Z)> crystals, (int X, int Y, int Z) corner, (int X, int Y, int Z) arrival, (int X, int Y, int Z) size)
    {
        if (corner.X < 1 || corner.X + DisplayWidth > size.X || corner.Y < 1 || corner.Y + DisplayHeight > size.Y
            || corner.Z < 1 || corner.Z + DisplayDepth > size.Z)
            return false;

        for (var x = corner.X; x < corner.X + DisplayWidth; x++)
        for (var z = corner.Z; z < corner.Z + DisplayDepth; z++)
        {
            if ((Math.Abs(x - arrival.X) <= ArrivalClearance && Math.Abs(z - arrival.Z) <= ArrivalClearance)
                || !voxels.Contains((x, corner.Y - 1, z))
                || Enumerable.Range(0, DisplayHeight).Any(height => voxels.Contains((x, corner.Y + height, z)))
                || NearEntity(entities, x, corner.Y, z)
                || veins.Any(vein => Math.Abs(vein.X - x) <= 1 && Math.Abs(vein.Z - z) <= 1)
                || crystals.Any(crystal => Math.Abs(crystal.X - x) <= 1 && Math.Abs(crystal.Z - z) <= 1))
                return false;
        }

        return true;
    }

    private static void PlaceAt(SkySagaDbContext db, World island, int x, int y, int z)
    {
        AddDevice(db, island, "Mushroom_Cave_A", (x, y, z), OnFloor, "Food_Green");
        AddDevice(db, island, "Mushroom_Cave_B", (x + 2, y, z), OnFloor, "Food_Green");
        db.VoxelEdits.Add(new VoxelEdit { WorldId = island.Id, X = x + 4, Y = y, Z = z, Block = GlowingMushroomBlock });
        db.VoxelEdits.Add(new VoxelEdit { WorldId = island.Id, X = x + 6, Y = y, Z = z, Block = GlowingCrystalBlock });

        for (var i = 0; i < Jewels.Length - 1; i++)
            AddDevice(db, island, "Cave_Crystal_Clump_A_Lit", (x + 8 + i * 2, y, z), OnFloor, Jewels[i]);

        for (var i = 0; i < Jewels.Length; i++)
            AddDevice(db, island, "Cave_Crystal_Clump_A", (x + 2 + i * 3, y, z + 1), OnFloor, Jewels[i]);

        for (var i = 0; i < LargeCrystalJewels.Length; i++)
            AddDevice(db, island, "Cave_Crystal_Clump_Large", (x + 2 + i * 3, y, z + 6), OnFloor, LargeCrystalJewels[i]);

        var ceiling = y + DisplayHeight - 1;

        for (var i = 0; i < Hanging.Length; i++)
        {
            var hangX = x + 1 + i * 2;
            db.VoxelEdits.Add(new VoxelEdit { WorldId = island.Id, X = hangX, Y = ceiling, Z = z + 3, Block = StoneBlock });
            AddDevice(db, island, Hanging[i].Item, (hangX, ceiling - Hanging[i].Height, z + 3), OnCeiling, Hanging[i].Materials);
        }
    }

    private static bool NearEntity(List<(int X, int Y, int Z)> entities, int x, int y, int z)
        => entities.Any(entity => Math.Abs(entity.X - x) <= 2 && Math.Abs(entity.Y - y) <= 2 && Math.Abs(entity.Z - z) <= 2);

    private static void AddDevice(SkySagaDbContext db, World island, string item, (int X, int Y, int Z) position, byte face, params string[] materials)
        => db.PlacedDevices.Add(new PlacedDevice
        {
            WorldId = island.Id,
            ItemCrc = InitialOreVeins.NameCrc(item),
            X = position.X,
            Y = position.Y,
            Z = position.Z,
            Materials = [.. materials.Select(material => (long?)InitialOreVeins.NameCrc(material))],
            Face = face
        });
}
