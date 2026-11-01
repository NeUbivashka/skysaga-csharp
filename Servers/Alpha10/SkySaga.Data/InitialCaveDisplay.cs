using SkySaga.Data.Entities;

namespace SkySaga.Data;

public static class InitialCaveDisplay
{
    private static readonly (long Item, long?[] Materials)[] Hanging =
    [
        (2257572654, [2436106595]), (4211841869, [2436106595]), (4290901064, [2436106595]),
        (292761574, []), (1017985238, []), (1748830336, []),
        (3464291397, [1536416095, 2436106595])
    ];

    public static bool Add(SkySagaDbContext db, World island, HashSet<(int X, int Y, int Z)> voxels,
        List<(int X, int Y, int Z)> entities, List<(int X, int Y, int Z)> veins,
        List<(int X, int Y, int Z)> crystals, (int X, int Y, int Z) spawn)
    {
        for (var radius = 7; radius <= 32; radius++)
        for (var dz = -radius; dz <= radius; dz++)
        for (var dx = -radius; dx <= radius; dx++)
        {
            if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != radius) continue;
            var x = spawn.X + dx; var y = spawn.Y; var z = spawn.Z + dz;
            if (x < 1 || x + 14 >= 192 || y < 1 || y + 5 >= 128 || z < 1 || z + 6 >= 192) continue;
            var valid = true;
            for (var ix = 0; ix < 15 && valid; ix++)
            for (var iz = 0; iz < 7 && valid; iz++)
            {
                var px = x + ix; var pz = z + iz;
                if (!voxels.Contains((px, y - 1, pz)) || Enumerable.Range(0, 5).Any(h => voxels.Contains((px, y + h, pz)))
                    || entities.Any(e => Math.Abs(e.X - px) <= 2 && Math.Abs(e.Y - y) <= 2 && Math.Abs(e.Z - pz) <= 2)
                    || veins.Any(p => Math.Abs(p.X - px) <= 1 && Math.Abs(p.Z - pz) <= 1)
                    || crystals.Any(p => Math.Abs(p.X - px) <= 1 && Math.Abs(p.Z - pz) <= 1)) valid = false;
            }
            if (!valid) continue;
            db.PlacedDevices.Add(new PlacedDevice { WorldId = island.Id, ItemCrc = 2683465497, X = x, Y = y, Z = z, Materials = [1869096730], Face = 2 });
            db.PlacedDevices.Add(new PlacedDevice { WorldId = island.Id, ItemCrc = 2461092288, X = x + 2, Y = y, Z = z, Materials = [1869096730], Face = 2 });
            db.VoxelEdits.Add(new VoxelEdit { WorldId = island.Id, X = x + 4, Y = y, Z = z, Block = 136 });
            db.VoxelEdits.Add(new VoxelEdit { WorldId = island.Id, X = x + 6, Y = y, Z = z, Block = 137 });
            long[] largeCrystalMaterials = [1536416095, 2315324841, 228787774, 2697292451, 3667810571];
            for (var i = 0; i < largeCrystalMaterials.Length; i++)
                db.PlacedDevices.Add(new PlacedDevice { WorldId = island.Id, ItemCrc = 1761600208, X = x + 2 + i * 3, Y = y, Z = z + 6, Materials = [largeCrystalMaterials[i]], Face = 2 });
            for (var i = 0; i < Hanging.Length; i++)
            {
                var hx = x + 1 + i * 2;
                db.VoxelEdits.Add(new VoxelEdit { WorldId = island.Id, X = hx, Y = y + 4, Z = z + 3, Block = 2 });
                db.PlacedDevices.Add(new PlacedDevice { WorldId = island.Id, ItemCrc = Hanging[i].Item, X = hx, Y = y + 3, Z = z + 3, Materials = Hanging[i].Materials, Face = 3 });
            }
            return true;
        }
        return false;
    }
}