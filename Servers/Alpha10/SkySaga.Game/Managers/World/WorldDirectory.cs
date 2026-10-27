using System.Linq;
using SkySaga.Data.Entities;
using SkySaga.Game.Database;

namespace SkySaga.Game.Managers.World;

public sealed class WorldDirectory : IDisposable
{
    private static readonly TimeSpan IdleLifetime = TimeSpan.FromMinutes(2);

    private readonly Dictionary<Guid, WorldInstance> _worlds = [];

    public IReadOnlyCollection<WorldInstance> Loaded => _worlds.Values;

    public WorldInstance Open(WorldInfo info)
    {
        if (_worlds.TryGetValue(info.Id, out var world))
            return world;

        world = new WorldInstance(info);
        _worlds[info.Id] = world;

        if (info.IsShared)
            WorldStore.Visited(info.Id);

        Console.WriteLine($"[Worlds] Loaded {info.Kind} {info.Id} ({info.Template}) owned by {info.OwnerName ?? "nobody"}. {_worlds.Count} world(s) loaded.");
        return world;
    }

    public WorldInfo? Find(Guid worldId)
        => _worlds.TryGetValue(worldId, out var world) ? world.Info : WorldStore.Load(worldId);

    public WorldInfo? EntryFor(Guid characterId)
    {
        if (WorldStore.Character(characterId) is not { } character)
            return null;

        if (character.WorldId is { } current && Find(current) is { } world && world.CanVisit(characterId))
            return world;

        var destination = character.Customised ? WorldStore.Hub() : WorldStore.Instance(WorldKind.CharacterCreation, characterId);

        WorldStore.SetCurrentWorld(characterId, destination.Id);
        return destination;
    }

    public void UnloadIdle()
    {
        var now = DateTime.UtcNow;

        foreach (var world in _worlds.Values.Where(w => w.IsEmpty && w.Info.Kind != WorldKind.Hub && now - w.EmptySince > IdleLifetime).ToList())
        {
            _worlds.Remove(world.Info.Id);
            world.Dispose();
            Console.WriteLine($"[Worlds] Unloaded idle {world.Info.Kind} {world.Info.Id}. {_worlds.Count} world(s) loaded.");
        }
    }

    public void Dispose()
    {
        foreach (var world in _worlds.Values)
            world.Dispose();

        _worlds.Clear();
    }
}
