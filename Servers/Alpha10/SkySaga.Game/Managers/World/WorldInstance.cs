using System.Linq;

namespace SkySaga.Game.Managers.World;

public sealed class WorldInstance : IDisposable
{
    private static readonly TimeSpan AutosaveInterval = TimeSpan.FromSeconds(15);

    private DateTime _lastAutosaveUtc = DateTime.UtcNow;

    public WorldInfo Info { get; }
    public PlayerConnectionManager Connections { get; } = new();
    public WorldManager Manager { get; }
    public PlayerInitializer PlayerInitializer { get; }
    public AiManager Ai { get; }
    public ChatCommands Commands { get; }
    public DateTime EmptySince { get; private set; } = DateTime.UtcNow;

    public WorldInstance(WorldInfo info)
    {
        Info = info;
        Manager = new WorldManager(info, Connections);
        PlayerInitializer = new PlayerInitializer(Manager);
        Ai = new AiManager(Manager, Connections);
        Commands = new ChatCommands(Manager, Connections);
    }

    public bool IsEmpty => !Connections.GetAllConnections().Any();

    public void Join(PlayerConnection connection)
    {
        Connections.AddConnection(connection.Guid.g, connection);
    }

    public void Leave(PlayerConnection connection)
    {
        Connections.RemoveConnection(connection.Guid.g);

        if (IsEmpty)
            EmptySince = DateTime.UtcNow;
    }

    public void Tick()
    {
        if (IsEmpty)
            return;

        Ai.Tick();
        SyncEntities();

        foreach (var connection in Connections.GetAllConnections().ToList())
            connection.Tick();

        Autosave();
    }

    private void SyncEntities()
    {
        var owners = Connections.GetAllConnections()
            .Where(connection => connection.IsPlayerInitialized)
            .ToDictionary(connection => connection.PlayerEntity.Id);

        foreach (var entity in Manager.EntityManager.Entities)
        {
            if (!entity.SyncRequired)
                continue;

            if (!owners.TryGetValue(entity.Id, out var owner))
            {
                Connections.BroadcastToAll(new EntitySync
                {
                    Id = entity.Id,
                    SyncData = entity.GetSyncData(newEntity: false, forOthers: true)
                });
                continue;
            }

            owner.Send(new EntitySync { Id = entity.Id, SyncData = entity.GetSyncData(newEntity: false, clearDirty: false) });
            Connections.BroadcastToAllExcept(owner.Guid.g, new EntitySync
            {
                Id = entity.Id,
                SyncData = entity.GetSyncData(newEntity: false, includeOwnerOnly: false, forOthers: true)
            });
        }

        Connections.FlushRemovedAfterSync();
    }

    private void Autosave()
    {
        var now = DateTime.UtcNow;

        if (now - _lastAutosaveUtc < AutosaveInterval)
            return;

        _lastAutosaveUtc = now;

        foreach (var connection in Connections.GetAllConnections())
            connection.SaveState();
    }

    public void Dispose()
    {
        Manager.Dispose();
    }
}
