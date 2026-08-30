namespace SkySaga.ChatCommunication;

/// <summary>
/// Simple IRC server implementation.
/// </summary>
public class IrcServer(ILogger<IrcServer> logger) : IDisposable
{
    private readonly int _port = 444;
    private readonly ILogger<IrcServer> _logger = logger;
    private TcpListener? _listener;
    private readonly Dictionary<string, IrcClient> _clients = [];
    private readonly Dictionary<string, IrcChannel> _channels = [];
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _serverTask;

    /// <summary>
    /// Starts the IRC server.
    /// </summary>
    public async Task StartAsync()
    {
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();
        _cancellationTokenSource = new CancellationTokenSource();

        _logger.LogInformation("IRC Server started on port {Port}", _port);

        _serverTask = AcceptClientsAsync(_cancellationTokenSource.Token);
        await _serverTask;
    }

    /// <summary>
    /// Stops the IRC server.
    /// </summary>
    public async Task StopAsync()
    {
        _cancellationTokenSource?.Cancel();
        _listener?.Stop();

        if (_serverTask != null)
        {
            try
            {
                await _serverTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
            }
        }

        _logger.LogInformation("IRC Server stopped");
    }

    private async Task AcceptClientsAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var tcpClient = await _listener!.AcceptTcpClientAsync(cancellationToken);
                var remoteEndPoint = tcpClient.Client.RemoteEndPoint?.ToString() ?? "Unknown";
                _logger.LogInformation("New client connection from {RemoteEndPoint}", remoteEndPoint);
                _ = HandleClientAsync(tcpClient, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelling
        }
    }

    private async Task HandleClientAsync(TcpClient tcpClient, CancellationToken cancellationToken)
    {
        var client = new IrcClient(tcpClient, this, _logger);
        await client.ProcessAsync(cancellationToken);
    }

    /// <summary>
    /// Registers a client with the server.
    /// </summary>
    public void RegisterClient(string nickname, IrcClient client)
    {
        _clients[nickname] = client;
        _logger.LogInformation("Client registered: {Nickname} (Total clients: {ClientCount})", nickname, _clients.Count);
    }

    /// <summary>
    /// Unregisters a client from the server.
    /// </summary>
    public void UnregisterClient(string nickname)
    {
        _clients.Remove(nickname);
        _logger.LogInformation("Client unregistered: {Nickname} (Total clients: {ClientCount})", nickname, _clients.Count);
    }

    /// <summary>
    /// Gets a client by nickname.
    /// </summary>
    public bool TryGetClient(string nickname, [NotNullWhen(true)] out IrcClient? client)
    {
        return _clients.TryGetValue(nickname, out client);
    }

    /// <summary>
    /// Gets or creates a channel.
    /// </summary>
    public IrcChannel GetOrCreateChannel(string name)
    {
        if (!_channels.TryGetValue(name, out var channel))
        {
            channel = new IrcChannel(name);
            _channels[name] = channel;
            _logger.LogInformation("New channel created: {ChannelName} (Total channels: {ChannelCount})", name, _channels.Count);
        }
        return channel;
    }

    /// <summary>
    /// Gets a channel by name.
    /// </summary>
    public bool TryGetChannel(string name, [NotNullWhen(true)] out IrcChannel? channel)
    {
        return _channels.TryGetValue(name, out channel);
    }

    /// <summary>
    /// Broadcasts a message to all clients.
    /// </summary>
    public async Task BroadcastAsync(string message)
    {
        _logger.LogDebug("Broadcasting message to {ClientCount} clients: {Message}", _clients.Count, message);
        var tasks = _clients.Values.Select(client => client.SendAsync(message)).ToList();
        await Task.WhenAll(tasks);
    }

    public void Dispose()
    {
        _listener?.Stop();
        _cancellationTokenSource?.Dispose();
        _serverTask?.Dispose();

        foreach (var client in _clients.Values)
        {
            client.Dispose();
        }
    }
}
