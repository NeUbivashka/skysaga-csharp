using System.Linq;

namespace SkySaga.Game.Managers.World;

public static class TreeGrowth
{
    public const byte TreeVoxel = 19;

    private const byte PlusX = 0, MinusX = 1, PlusZ = 2, MinusZ = 3, Up = 4, Same = 6;

    private static readonly Vector3Int[] Offsets =
        [new(1, 0, 0), new(-1, 0, 0), new(0, 0, 1), new(0, 0, -1), new(0, 1, 0), new(0, -1, 0), new(0, 0, 0), new(0, 1, 0)];

    private static readonly Dictionary<int, byte[]> JunctionArms = new()
    {
        [3] = [MinusZ],
        [4] = [PlusZ, MinusZ],
        [5] = [PlusX, MinusZ],
        [6] = [MinusX, PlusZ, MinusZ],
        [7] = [PlusX, MinusX, PlusZ, MinusZ]
    };

    private const int BaseMesh0 = 0, BaseMesh1 = 1, TrunkMesh = 2, TopMesh = 8, BranchMesh = 9, BranchEndMesh = 10,
        LeavesTopMesh = 12, LeavesMidMesh = 13, LeavesMidBMesh = 14, LeavesBranchMesh = 15;

    public readonly record struct Placed(TreePart Part, Vector3Int Block);

    public static List<Placed> Grow(TreeTypeInfo type, Random random) => type.Name switch
    {
        "PalmTree" => GrowPalm(type, random),
        "BambooTree" => GrowBamboo(type, random),
        "DesertCactus" => GrowCactus(type, random),
        _ => GrowForest(type, random)
    };

    private sealed class Builder(TreeTypeInfo type, Random random)
    {
        public readonly List<Placed> Parts = [];

        public int Add(TreePartInfo info, int parent, byte direction, bool decoration = false)
        {
            var block = parent < 0 ? new Vector3Int(0, 0, 0) : Parts[parent].Block + Offsets[direction];
            Parts.Add(new Placed(new TreePart(info.Id, direction, parent, 0, decoration, TreeVoxel), block));
            return Parts.Count - 1;
        }

        public List<TreePartInfo> Named(string mesh)
            => [.. type.Parts.Values.Where(part => part.MeshName.Equals(mesh, StringComparison.OrdinalIgnoreCase)).OrderBy(part => part.Id)];

        public List<TreePartInfo> Prefixed(string prefix)
            => [.. type.Parts.Values.Where(part => part.MeshName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).OrderBy(part => part.Id)];

        public TreePartInfo? Pick(List<TreePartInfo> options) => options.Count == 0 ? null : options[random.Next(options.Count)];

        public void Crown(int top, List<TreePartInfo> leaves)
        {
            foreach (var yaw in new[] { 0f, 90f, 180f, -90f })
            {
                var meshes = leaves.Select(part => part.MeshName).Distinct().ToList();

                if (meshes.Count == 0)
                    return;

                var mesh = meshes[random.Next(meshes.Count)];

                if (leaves.FirstOrDefault(part => part.MeshName == mesh && part.Rotation.Y == yaw) is { } frond)
                    Add(frond, top, Same, true);
            }
        }
    }

    private static List<Placed> GrowPalm(TreeTypeInfo type, Random random)
    {
        var tree = new Builder(type, random);

        if (tree.Pick(tree.Prefixed("ScatterPalmTreeBase")) is not { } basePart || tree.Pick(tree.Named("ScatterPalmTreeMid")) is not { } mid)
            return tree.Parts;

        var current = tree.Add(basePart, -1, 0);

        for (int level = 0, height = 4 + random.Next(5); level < height; level++)
            current = tree.Add(mid, current, Up);

        if (tree.Pick(tree.Prefixed("ScatterPalmTreeTop")) is { } top)
            current = tree.Add(top, current, Up);

        tree.Crown(current, tree.Prefixed("ScatterPalmTreeLeaf"));

        return tree.Parts;
    }

    private static List<Placed> GrowBamboo(TreeTypeInfo type, Random random)
    {
        var tree = new Builder(type, random);
        var yaw = new[] { 0f, 90f, 180f, -90f }[random.Next(4)];

        TreePartInfo? Facing(string mesh) => tree.Named(mesh).FirstOrDefault(part => part.Rotation.Y == yaw);

        if (Facing("ScatterBambooTreeBase") is not { } basePart || Facing("ScatterBambooTreeMid") is not { } mid)
            return tree.Parts;

        var current = tree.Add(basePart, -1, 0);
        var rings = tree.Prefixed("ScatterBambooTreeLeafMid");

        for (int level = 0, height = 5 + random.Next(5); level < height; level++)
        {
            current = tree.Add(mid, current, Up);

            if (level > 1 && level % 2 == 0 && tree.Pick(rings) is { } ring)
                tree.Add(ring, current, Same, true);
        }

        if (Facing("ScatterBambooTreeTop") is { } top)
            current = tree.Add(top, current, Up);

        tree.Crown(current, [.. tree.Prefixed("ScatterBambooTreeLeaf").Where(part => !part.MeshName.Contains("Mid", StringComparison.OrdinalIgnoreCase))]);

        return tree.Parts;
    }

    private static List<Placed> GrowCactus(TreeTypeInfo type, Random random)
    {
        var tree = new Builder(type, random);

        if (tree.Pick(tree.Named("ScatterDesertCactusTreeTrunk")) is not { } trunk)
            return tree.Parts;

        var junctions = tree.Named("ScatterDesertCactusTreeBranchJunction");
        var corners = tree.Named("ScatterDesertCactusTreeBranchCorner");
        var upright = tree.Named("ScatterDesertCactusTreeBranch").Where(part => part.Rotation.X == 0 && part.Rotation.Z == 0).ToList();
        var height = 4 + random.Next(4);
        var armLevel = 1 + random.Next(Math.Max(1, height - 3));
        var current = tree.Add(trunk, -1, 0);

        for (var level = 1; level < height; level++)
        {
            if (level == armLevel && tree.Pick(junctions) is { } junction)
            {
                current = tree.Add(junction, current, Up);
                var arm = Turn(Direction(-MathF.Sin(junction.Rotation.Z * MathF.PI / 180f), 0f), junction.Rotation.Y);
                var outward = Opposite(arm);

                if (corners.FirstOrDefault(corner => Turn(PlusX, corner.Rotation.Y) == outward) is { } corner)
                {
                    var branch = tree.Add(corner, current, arm);

                    for (int step = 0, length = 1 + random.Next(2); step < length && tree.Pick(upright) is { } piece; step++)
                        branch = tree.Add(piece, branch, Up);

                    if (tree.Pick(tree.Named("ScatterDesertCactusTreeFlower")) is { } flower)
                        tree.Add(flower, branch, Same, true);
                }

                continue;
            }

            current = tree.Add(trunk, current, Up);
        }

        if (tree.Pick(tree.Named("ScatterDesertCactusTreeLeavesTop")) is { } cap)
            tree.Add(cap, current, Same, true);

        return tree.Parts;
    }

    private static byte Opposite(byte direction) => direction switch
    {
        PlusX => MinusX,
        MinusX => PlusX,
        PlusZ => MinusZ,
        MinusZ => PlusZ,
        _ => direction
    };

    private static List<Placed> GrowForest(TreeTypeInfo type, Random random)
    {
        var parts = new List<Placed>();
        var byMesh = type.Parts.Values.GroupBy(part => part.Mesh).ToDictionary(group => group.Key, group => group.ToList());

        int Add(TreePartInfo info, int parent, byte direction, bool decoration)
        {
            var block = parent < 0 ? new Vector3Int(0, 0, 0) : parts[parent].Block + Offsets[direction];
            parts.Add(new Placed(new TreePart(info.Id, direction, parent, 0, decoration, TreeVoxel), block));
            return parts.Count - 1;
        }

        TreePartInfo? Pick(int mesh) => byMesh.TryGetValue(mesh, out var list) ? list[random.Next(list.Count)] : null;

        TreePartInfo? Facing(int mesh, byte direction)
            => byMesh.TryGetValue(mesh, out var list) ? list.FirstOrDefault(part => Pointing(part) == direction) : null;

        var basePart = Pick(random.Next(2) == 0 ? BaseMesh0 : BaseMesh1) ?? Pick(BaseMesh0) ?? Pick(TrunkMesh);

        if (basePart is null)
            return parts;

        var current = Add(basePart, -1, 0, false);
        var height = 7 + random.Next(9);
        var nextTier = 3 + random.Next(2);
        var tier = 0;
        var trunk = Pick(TrunkMesh);
        var branching = byMesh.ContainsKey(4) || byMesh.ContainsKey(7);
        var ringStart = branching ? int.MaxValue : height / 3 + random.Next(2);

        for (var level = 1; level < height && trunk is not null; level++)
        {
            if (level == nextTier && height - level > 2 && Junction(tier) is { } junction)
            {
                current = Add(junction, current, Up, false);
                var length = tier == 0 ? 2 : 1;

                foreach (var arm in Arms(junction))
                {
                    var branch = current;

                    for (var step = 1; step < length && Facing(BranchMesh, arm) is { } section; step++)
                        branch = Add(section, branch, arm, false);

                    if (Facing(BranchEndMesh, arm) is { } end)
                    {
                        branch = Add(end, branch, arm, false);

                        if (Facing(LeavesBranchMesh, arm) is { } cluster)
                            Add(cluster, branch, Same, true);
                    }
                }

                tier++;
                nextTier = level + 2 + random.Next(2);
                continue;
            }

            current = Add(trunk, current, Up, false);

            if ((tier > 0 || level >= ringStart) && (Pick((branching ? tier : level) % 2 == 0 ? LeavesMidMesh : LeavesMidBMesh) ?? Pick(LeavesMidMesh)) is { } ring)
                Add(ring, current, Same, true);
        }

        if (Pick(TopMesh) is { } top)
        {
            current = Add(top, current, Up, false);

            if (Pick(LeavesTopMesh) is { } leaves)
                Add(leaves, current, type.Name == "MountainTree" ? Same : Up, true);
        }

        return parts;

        TreePartInfo? Junction(int index)
        {
            var mesh = index % 2 == 0 ? 7 : 4;
            var options = byMesh.TryGetValue(mesh, out var list) ? list : null;
            return options is null ? null : options[index / 2 % options.Count];
        }
    }

    public static Vector3Int Offset(byte direction) => Offsets[direction];

    private static byte[] Arms(TreePartInfo junction)
        => JunctionArms.TryGetValue(junction.Mesh, out var arms) ? [.. arms.Select(arm => Turn(arm, junction.Rotation.Y))] : [];

    private static byte Pointing(TreePartInfo part)
    {
        if (part.Mesh is BranchMesh or BranchEndMesh)
        {
            var x = part.Rotation.X * MathF.PI / 180f;
            var z = part.Rotation.Z * MathF.PI / 180f;
            return Direction(-MathF.Sin(z) * MathF.Cos(x), MathF.Sin(x));
        }

        return Turn(PlusX, part.Rotation.Y);
    }

    private static byte Turn(byte direction, float yawDegrees)
    {
        var offset = Offsets[direction];
        var yaw = yawDegrees * MathF.PI / 180f;
        return Direction(offset.X * MathF.Cos(yaw) + offset.Z * MathF.Sin(yaw), -offset.X * MathF.Sin(yaw) + offset.Z * MathF.Cos(yaw));
    }

    private static byte Direction(float x, float z)
        => MathF.Abs(x) >= MathF.Abs(z) ? (x >= 0 ? PlusX : MinusX) : (z >= 0 ? PlusZ : MinusZ);
}
