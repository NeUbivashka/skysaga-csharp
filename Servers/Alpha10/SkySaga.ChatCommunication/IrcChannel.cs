namespace SkySaga.ChatCommunication;

/// <summary>
/// Represents an IRC channel.
/// </summary>
public class IrcChannel(string name)
{
    private readonly string _name = name;
    private readonly List<IrcClient> _clients = [];

    /// <summary>
    /// Gets the channel name.
    /// </summary>
    public string Name => _name;

    /// <summary>
    /// Adds a client to the channel.
    /// </summary>
    public void AddClient(IrcClient client)
    {
        if (!_clients.Contains(client))
        {
            _clients.Add(client);
        }
    }

    /// <summary>
    /// Removes a client from the channel.
    /// </summary>
    public void RemoveClient(IrcClient client)
    {
        _clients.Remove(client);
    }

    /// <summary>
    /// Broadcasts a message to all clients in the channel.
    /// </summary>
    public async Task BroadcastAsync(string message, IrcClient? excludeClient = null)
    {
        var tasks = _clients
            .Where(client => client != excludeClient)
            .Select(client => client.SendAsync(message))
            .ToList();

        await Task.WhenAll(tasks);
    }
}
