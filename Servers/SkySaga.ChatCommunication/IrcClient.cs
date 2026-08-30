namespace SkySaga.ChatCommunication;

/// <summary>
/// Represents a single IRC client connection.
/// </summary>
public class IrcClient : IDisposable
{
    private readonly TcpClient _tcpClient;
    private readonly IrcServer _server;
    private readonly ILogger _logger;
    private readonly StreamWriter _writer;
    private readonly StreamReader _reader;
    private string? _nickname;
    private string? _username;
    private bool _isRegistered;
    private readonly List<IrcChannel> _channels = [];

    public IrcClient(TcpClient tcpClient, IrcServer server, ILogger logger)
    {
        _tcpClient = tcpClient;
        _server = server;
        _logger = logger;

        var networkStream = tcpClient.GetStream();
        _writer = new StreamWriter(networkStream, Encoding.UTF8) { AutoFlush = true };
        _reader = new StreamReader(networkStream, Encoding.UTF8);
    }

    /// <summary>
    /// Gets the client's nickname.
    /// </summary>
    public string? Nickname => _nickname;

    /// <summary>
    /// Processes incoming messages from the client.
    /// </summary>
    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Client session started");

        try
        {
            await SendAsync(":server NOTICE AUTH :*** Welcome to IRC server ***");

            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await _reader.ReadLineAsync(cancellationToken);
                if (line == null)
                {
                    _logger.LogInformation("Client connection closed (received null line)");
                    break;
                }

                _logger.LogDebug("Received from {Nickname}: {Line}", _nickname ?? "Unknown", line);
                await HandleCommandAsync(line);
            }
        }
        catch (IOException ex)
        {
            _logger.LogInformation(ex, "Client {Nickname} disconnected (IO error)", _nickname ?? "Unknown");
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogInformation(ex, "Client {Nickname} connection cancelled (server shutting down)", _nickname ?? "Unknown");
        }
        finally
        {
            Disconnect();
        }
    }

    private async Task HandleCommandAsync(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return;

        var command = parts[0].ToUpperInvariant();
        _logger.LogDebug("Processing command: {Command} from {Nickname}", command, _nickname ?? "Unknown");

        switch (command)
        {
            case "NICK":
                await HandleNickAsync(parts);
                break;
            case "USER":
                await HandleUserAsync(parts);
                break;
            case "JOIN":
                await HandleJoinAsync(parts);
                break;
            case "PART":
                await HandlePartAsync(parts);
                break;
            case "PRIVMSG":
                await HandlePrivmsgAsync(parts, line);
                break;
            case "QUIT":
                await HandleQuitAsync();
                break;
            case "PING":
                await HandlePingAsync(parts);
                break;
            default:
                _logger.LogWarning("Unknown command from {Nickname}: {Command}", _nickname ?? "Unknown", command);
                await SendAsync($":server 421 {_nickname} {command} :Unknown command");
                break;
        }
    }

    private async Task HandleNickAsync(string[] parts)
    {
        if (parts.Length < 2)
        {
            _logger.LogWarning("NICK command without nickname");
            await SendAsync(":server 431 :No nickname given");
            return;
        }

        var newNick = parts[1];
        var oldNick = _nickname;

        if (_nickname != null)
        {
            _server.UnregisterClient(_nickname);
        }

        _nickname = newNick;
        _server.RegisterClient(newNick, this);
        _logger.LogInformation("Nickname changed: {OldNick} -> {NewNick}", oldNick ?? "None", newNick);

        if (_isRegistered)
        {
            await BroadcastToChannelsAsync($":{_nickname} NICK {newNick}");
        }
        else if (_username != null)
        {
            await RegisterClientAsync();
        }
    }

    private async Task HandleUserAsync(string[] parts)
    {
        if (parts.Length < 5)
        {
            _logger.LogWarning("USER command with insufficient parameters");
            await SendAsync(":server 461 USER :Not enough parameters");
            return;
        }

        _username = parts[1];
        _logger.LogInformation("Username set: {Username} for client {Nickname}", _username, _nickname ?? "Unknown");

        if (_nickname != null && !_isRegistered)
        {
            await RegisterClientAsync();
        }
    }

    private async Task RegisterClientAsync()
    {
        _isRegistered = true;
        _logger.LogInformation("Client registered: {Nickname} / {Username}", _nickname, _username);

        await SendAsync($":server 001 {_nickname} :Welcome to the IRC server");
        //await SendAsync($":server 002 {_nickname} :Your host is server");
        //await SendAsync($":server 003 {_nickname} :This server was created");
        //await SendAsync($":server 004 {_nickname} server 1.0 i mt");
    }

    private async Task HandleJoinAsync(string[] parts)
    {
        if (parts.Length < 2)
        {
            _logger.LogWarning("JOIN command from {Nickname} without channel name", _nickname ?? "Unknown");
            return;
        }

        if (!_isRegistered || _nickname == null)
        {
            _logger.LogWarning("JOIN command from unregistered client");
            return;
        }

        var channelName = parts[1];
        if (!channelName.StartsWith("#"))
            channelName = "#" + channelName;

        var channel = _server.GetOrCreateChannel(channelName);
        channel.AddClient(this);
        _channels.Add(channel);
        _logger.LogInformation("Client {Nickname} joined channel {ChannelName}", _nickname, channelName);

        await SendAsync($":{_nickname} JOIN {channelName}");
        await BroadcastToChannelAsync(channelName, $":{_nickname} JOIN {channelName}", excludeSelf: false);
    }

    private async Task HandlePartAsync(string[] parts)
    {
        if (parts.Length < 2)
        {
            _logger.LogWarning("PART command from {Nickname} without channel name", _nickname ?? "Unknown");
            return;
        }

        if (_nickname == null)
        {
            _logger.LogWarning("PART command from unregistered client");
            return;
        }

        var channelName = parts[1];
        if (_server.TryGetChannel(channelName, out var channel))
        {
            channel.RemoveClient(this);
            _channels.Remove(channel);
            _logger.LogInformation("Client {Nickname} left channel {ChannelName}", _nickname, channelName);
            await BroadcastToChannelAsync(channelName, $":{_nickname} PART {channelName}");
        }
    }

    private async Task HandlePrivmsgAsync(string[] parts, string line)
    {
        if (parts.Length < 2)
        {
            _logger.LogWarning("PRIVMSG command from {Nickname} without target", _nickname ?? "Unknown");
            return;
        }

        if (_nickname == null)
        {
            _logger.LogWarning("PRIVMSG command from unregistered client");
            return;
        }

        var target = parts[1];
        var messageStart = line.IndexOf(':', parts.Length > 2 ? 2 : 1);
        if (messageStart < 0)
        {
            _logger.LogWarning("PRIVMSG command from {Nickname} without message", _nickname);
            return;
        }

        var message = line[(messageStart + 1)..];

        if (target.StartsWith("#"))
        {
            // Channel message
            _logger.LogDebug("Channel message from {Nickname} to {Channel}: {Message}", _nickname, target, message);
            await BroadcastToChannelAsync(target, $":{_nickname} PRIVMSG {target} :{message}", excludeSelf: false);
        }
        else
        {
            // Private message
            if (_server.TryGetClient(target, out var targetClient))
            {
                _logger.LogDebug("Private message from {Nickname} to {Target}: {Message}", _nickname, target, message);
                await targetClient.SendAsync($":{_nickname} PRIVMSG {target} :{message}");
            }
            else
            {
                _logger.LogWarning("Private message from {Nickname} to unknown client {Target}", _nickname, target);
            }
        }
    }

    private async Task HandleQuitAsync()
    {
        _logger.LogInformation("Client {Nickname} quit", _nickname ?? "Unknown");
        await HandlePartAsync(["PART", "all"]);
        Disconnect();
    }

    private async Task HandlePingAsync(string[] parts)
    {
        var param = parts.Length > 1 ? parts[1] : "server";
        _logger.LogDebug("PING from {Nickname} with parameter {Param}", _nickname ?? "Unknown", param);
        await SendAsync($":server PONG {param}");
    }

    private async Task BroadcastToChannelsAsync(string message)
    {
        var tasks = _channels.Select(channel => BroadcastToChannelAsync(channel.Name, message, excludeSelf: true)).ToList();
        await Task.WhenAll(tasks);
    }

    private async Task BroadcastToChannelAsync(string channelName, string message, bool excludeSelf = true)
    {
        if (_server.TryGetChannel(channelName, out var channel))
        {
            await channel.BroadcastAsync(message, excludeClient: excludeSelf ? this : null);
        }
    }

    /// <summary>
    /// Sends a message to this client.
    /// </summary>
    public async Task SendAsync(string message)
    {
        try
        {
            _logger.LogDebug("Sending to {Nickname}: {Message}", _nickname ?? "Unknown", message);
            await _writer.WriteLineAsync(message);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Failed to send message to {Nickname}", _nickname ?? "Unknown");
            Disconnect();
        }
    }

    private void Disconnect()
    {
        _logger.LogInformation("Disconnecting client: {Nickname}", _nickname ?? "Unknown");

        if (_nickname != null)
        {
            _server.UnregisterClient(_nickname);
        }

        foreach (var channel in _channels.ToList())
        {
            channel.RemoveClient(this);
        }

        _channels.Clear();
    }

    public void Dispose()
    {
        _reader?.Dispose();
        _writer?.Dispose();
        _tcpClient?.Dispose();
    }
}
