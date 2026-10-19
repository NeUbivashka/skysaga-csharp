using System.Globalization;
using System.Linq;

namespace SkySaga.Game;

public class ChatCommands(IWorldManager worldManager, PlayerConnectionManager playerConnectionManager)
{
    private const float EyeHeight = 1.6f;
    private const float LookRange = 64f;
    private const float LookStep = 0.25f;
    private const float FallbackDistance = 4f;
    private const int ChunkSize = 32;
    private const int MaxSpawnCount = 20;

    public string Execute(PlayerConnection connection, string text)
    {
        var args = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (args.Length == 0)
            return Help();

        return args[0].ToLowerInvariant() switch
        {
            "help" => Help(),
            "heal" => Heal(connection),
            "kill" => Kill(connection),
            "tp" => Teleport(connection, args),
            "give" => Give(connection, args),
            "spawn" => Spawn(connection, args),
            "time" => Time(args),
            "xp" => Experience(connection, args),
            "recipe" => LearnRecipe(connection, args),
            _ => $"Unknown command {args[0]}. {Help()}"
        };
    }

    private static string Help()
        => "Commands: !heal, !kill, !tp <x> <y> <z>, !give <item> [count], !spawn <creature> [count], !time [hh[:mm] | freeze | run], !xp <amount>, !recipe <name | all>";

    private static string LearnRecipe(PlayerConnection connection, string[] args)
    {
        if (args.Length < 2 || !connection.PlayerEntity.TryGetComponent<RecipeBookComponent>(out var book))
            return "Usage: !recipe <name | all>";

        var all = args[1].Equals("all", StringComparison.OrdinalIgnoreCase);
        var recipes = all ? RecipeTable.All : RecipeTable.TryGetByName(args[1], out var recipe) ? [recipe] : [];

        if (recipes.Count == 0)
            return $"Unknown recipe {args[1]}.";

        var learned = recipes.Where(entry => !book.Knows(entry.WireIndex)).ToList();

        book.RecipeList = [.. book.RecipeList, .. learned.Select(entry => new KnownRecipe(entry.WireIndex, entry.Variants))];

        return all ? $"Learned {learned.Count} recipes." : $"Learned {recipes[0].Name}.";
    }

    private static string Experience(PlayerConnection connection, string[] args)
    {
        if (args.Length < 2 || !uint.TryParse(args[1], out var amount) || amount == 0)
            return "Usage: !xp <amount>";

        return JobExperience.Award(connection, amount) ? $"Gained {amount} XP." : "No XP was gained.";
    }

    private string Time(string[] args)
    {
        var timeOfDay = worldManager.EntityManager.Entities
            .Select(entity => entity.TryGetComponent<ClientTimeOfDayComponent>(out var component) ? component : null)
            .FirstOrDefault(component => component is not null);

        if (timeOfDay is null)
            return "The world has no time of day.";

        var now = RakNet.RakNet.GetTime();

        if (args.Length > 1)
        {
            switch (args[1].ToLowerInvariant())
            {
                case "freeze":
                    timeOfDay.SetFrozen(true, now);
                    break;
                case "run":
                    timeOfDay.SetFrozen(false, now);
                    break;
                default:
                    var parts = args[1].Split(':');

                    if (!float.TryParse(parts[0], CultureInfo.InvariantCulture, out var hours)
                        || parts.Length > 1 && !int.TryParse(parts[1], out _)
                        || hours < 0 || hours >= 24)
                        return "Usage: !time [hh[:mm] | freeze | run]";

                    var minutes = parts.Length > 1 ? int.Parse(parts[1]) : 0;

                    timeOfDay.SetTime((int)Math.Round((hours + minutes / 60f) / 24f * TimeOfDayComponent.DayLength), now);
                    break;
            }
        }

        var current = timeOfDay.CurrentTime(now) * 24f / TimeOfDayComponent.DayLength;
        var shown = TimeSpan.FromHours(current);

        return $"Time {shown.Hours:00}:{shown.Minutes:00}, {(timeOfDay.IsFrozen ? "frozen" : "running")}.";
    }

    private static string Heal(PlayerConnection connection)
    {
        if (!connection.PlayerEntity.TryGetComponent<ClientHealthComponent>(out var health))
            return "The player has no health.";

        health.Revive();

        return "Healed.";
    }

    private static string Kill(PlayerConnection connection)
    {
        if (!connection.PlayerEntity.TryGetComponent<ClientHealthComponent>(out var health))
            return "The player has no health.";

        health.Damage(int.MaxValue / 2);

        return "Killed.";
    }

    private static string Teleport(PlayerConnection connection, string[] args)
    {
        if (args.Length < 4
            || !float.TryParse(args[1], CultureInfo.InvariantCulture, out var x)
            || !float.TryParse(args[2], CultureInfo.InvariantCulture, out var y)
            || !float.TryParse(args[3], CultureInfo.InvariantCulture, out var z))
            return "Usage: !tp <x> <y> <z>";

        var position = new Vector3(x, y, z);

        connection.Teleport(position, connection.LookYaw);

        return $"Teleported to {position}.";
    }

    private string Give(PlayerConnection connection, string[] args)
    {
        if (args.Length < 2)
            return "Usage: !give <item> [count]";

        var count = args.Length > 2 && int.TryParse(args[2], out var parsed) ? Math.Clamp(parsed, 1, 999) : 1;

        if (ItemTable.IndexOf(args[1]) == 0)
            return $"Unknown item {args[1]}.";

        if (!InventoryItems.Give(connection, Util.ComputeCrc32(args[1]), count))
            return "The inventory is full.";

        return $"Gave {count} x {args[1]}.";
    }

    private string Spawn(PlayerConnection connection, string[] args)
    {
        if (args.Length < 2)
            return "Usage: !spawn <creature> [count], !time [hh[:mm] | freeze | run]";

        var count = args.Length > 2 && int.TryParse(args[2], out var parsed) ? Math.Clamp(parsed, 1, MaxSpawnCount) : 1;
        var position = LookTarget(connection);

        for (var i = 0; i < count; i++)
        {
            var entities = worldManager.SpawnCreature(args[1], position + new Vector3(i % 3, 0, i / 3));

            if (entities.Count == 0)
                return $"Unknown creature {args[1]}.";

            foreach (var entity in entities)
                Broadcast(entity);
        }

        return $"Spawned {count} x {args[1]} at {position} (yaw {connection.LookYaw:0}, pitch {connection.LookPitch:0}).";
    }

    private Vector3 LookTarget(PlayerConnection connection)
    {
        var origin = connection.PlayerEntity.TryGetComponent<SmoothedTransformComponent>(out var transform)
            ? transform.Position + new Vector3(0, EyeHeight, 0)
            : Vector3.Zero;

        var yaw = connection.LookYaw * MathF.PI / 180f;
        var pitch = connection.LookPitch * MathF.PI / 180f;
        var direction = new Vector3(MathF.Cos(pitch) * MathF.Sin(yaw), MathF.Sin(pitch), MathF.Cos(pitch) * MathF.Cos(yaw));

        for (var distance = LookStep; distance <= LookRange; distance += LookStep)
        {
            var point = origin + direction * distance;
            var voxel = new Vector3Int((int)MathF.Floor(point.X), (int)MathF.Floor(point.Y), (int)MathF.Floor(point.Z));

            if (voxel.Y >= 0 && IsSolid(voxel))
                return new Vector3(voxel.X + 0.5f, voxel.Y + 1, voxel.Z + 0.5f);
        }

        var ahead = origin + new Vector3(direction.X, 0, direction.Z) * FallbackDistance;

        return new Vector3(ahead.X, origin.Y - EyeHeight, ahead.Z);
    }

    private bool IsSolid(Vector3Int voxel)
    {
        var chunk = new Vector3Int(FloorDiv(voxel.X), FloorDiv(voxel.Y), FloorDiv(voxel.Z));
        var local = new Vector3Int(voxel.X - chunk.X * ChunkSize, voxel.Y - chunk.Y * ChunkSize, voxel.Z - chunk.Z * ChunkSize);

        return worldManager.ChunkManager.TryGetChunk(chunk, out _) && worldManager.ChunkManager.GetVoxel(chunk, local) != WorldChunk.Air;
    }

    private static int FloorDiv(int value) => (int)MathF.Floor(value / (float)ChunkSize);

    private void Broadcast(Entity entity)
        => playerConnectionManager.BroadcastToAll(new EntityAdd
        {
            Id = entity.Id,
            NameHash = Util.ComputeCrc32(entity.Name),
            SyncData = entity.GetSyncData(newEntity: true)
        });
}
