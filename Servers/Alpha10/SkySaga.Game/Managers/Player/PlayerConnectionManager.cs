namespace SkySaga.Game.Managers.Player;

public sealed class PlayerConnectionManager
{
    private readonly Dictionary<ulong, PlayerConnection> _connections = [];
    private readonly List<int> _removedAfterSync = [];

    public void AddConnection(ulong guid, PlayerConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connections.TryAdd(guid, connection);
    }

    public void RemoveConnection(ulong guid)
    {
        _connections.Remove(guid);
    }

    public bool TryGetConnection(ulong guid, [NotNullWhen(true)] out PlayerConnection? connection)
    {
        return _connections.TryGetValue(guid, out connection);
    }

    public IEnumerable<PlayerConnection> GetAllConnections()
    {
        return _connections.Values;
    }

    public void BroadcastRemovedAfterSync(int entityId) => _removedAfterSync.Add(entityId);

    public void FlushRemovedAfterSync()
    {
        foreach (var entityId in _removedAfterSync)
            BroadcastToAll(new EntityRemoved { Id = entityId });

        _removedAfterSync.Clear();
    }

    public void BroadcastToAll(ISerializablePacket packet)
    {
        foreach (var connection in _connections.Values)
        {
            connection.Send(packet);
        }
    }

    public void BroadcastToAllExcept(ulong excludeGuid, ISerializablePacket packet)
    {
        foreach (var kvp in _connections)
        {
            if (kvp.Key != excludeGuid)
            {
                kvp.Value.Send(packet);
            }
        }
    }
}
