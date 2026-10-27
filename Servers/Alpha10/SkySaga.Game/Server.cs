using RakNet;
using SkySaga.Game.Managers.Player;
using System.Linq;

namespace SkySaga.Game;

public class Server : IDisposable
{
    private const int MaxConnections = 100;

    private readonly string _password;
    private readonly RakPeerInterface _peer;

    private readonly Dictionary<ulong, PlayerConnection> _connections = [];
    private readonly System.Collections.Concurrent.ConcurrentQueue<(string Nickname, string Text, TaskCompletionSource<string?> Reply)> _commands = new();

    public Guid Id { get; } = Guid.NewGuid();
    public ushort Port { get; }
    public WorldDirectory Worlds { get; } = new();

    public Server(string password = "Something about penguins\0", ushort port = 42069)
    {
        _password = password ?? throw new ArgumentNullException(nameof(password));
        Port = port;

        _peer = RakPeerInterface.GetInstance();

        _peer.SetIncomingPassword(_password, _password.Length);
        _peer.SetMaximumIncomingConnections(MaxConnections);
    }

    public bool Start()
    {
        var status = _peer.Startup(MaxConnections, new SocketDescriptor(Port, string.Empty), 1);

        return status == StartupResult.RAKNET_STARTED;
    }

    public void ResetWorld()
    {
        _resetRequested = true;
    }

    private volatile bool _resetRequested;
    private volatile bool _listChickensRequested;

    public void Tick()
    {
        if (_resetRequested)
        {
            _resetRequested = false;

            foreach (var world in Worlds.Loaded)
                world.Manager.Reset();

            return;
        }

        if (_listChickensRequested)
        {
            _listChickensRequested = false;
            ListChickens();
        }

        ProcessPackets();
        ProcessCommands();

        foreach (var world in Worlds.Loaded.ToList())
        {
            try
            {
                world.Tick();
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[Worlds] {world.Info.Kind} {world.Info.Id} failed to tick: {exception}");
            }
        }

        Worlds.UnloadIdle();
    }

    public Task<string?> RunCommandAsync(string nickname, string text)
    {
        var reply = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        _commands.Enqueue((nickname, text, reply));

        return reply.Task;
    }

    private void ProcessCommands()
    {
        while (_commands.TryDequeue(out var command))
        {
            try
            {
                var players = _connections.Values.Where(c => c.IsPlayerInitialized).ToList();
                var connection = players.FirstOrDefault(c => string.Equals(c.CharacterName, command.Nickname, StringComparison.OrdinalIgnoreCase))
                    ?? players.FirstOrDefault();

                command.Reply.SetResult(connection?.World is not { } world ? "No player in the world." : world.Commands.Execute(connection, command.Text));
            }
            catch (Exception exception)
            {
                command.Reply.SetResult($"Error: {exception.Message}");
            }
        }
    }

    public void RequestListChickens()
    {
        _listChickensRequested = true;
    }

    private void ListChickens()
    {
        Console.WriteLine("--- Chickens ---");

        int count = 0;

        foreach (var entity in Worlds.Loaded.SelectMany(world => world.Manager.EntityManager.Entities))
        {
            if (!entity.TryGetComponent<ClientEnemyAiComponent>(out var ai))
                continue;

            if (!string.Equals(ai.AiCapabilities, "chicken", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!entity.TryGetComponent<SmoothedTransformComponent>(out var transform))
                continue;

            count++;

            float traveled = Vector3.Distance(transform.Position, ai.SpawnPosition);

            Console.WriteLine(
                $"  #{entity.Id}: pos={transform.Position}, spawn={ai.SpawnPosition}, traveled={traveled:F1}, state={ai.StateName}");
        }

        Console.WriteLine($"--- {count} chickens total ---");
    }

    private void ProcessPackets()
    {
        const int maxPacketsPerTick = 512;

        for (int i = 0; i < maxPacketsPerTick; i++)
        {
            var packet = _peer.Receive();

            if (packet is null)
                return;

            var bitStream = new BitStream(packet.data, packet.length, true);

            var messageId = bitStream.ReadMessageId(out var messageIdWasExtended);

            var fullRawHex = string.Join(" ", packet.data.Take((int)packet.length).Select(b => b.ToString("x2")));

            if (messageId < (byte)DefaultMessageIDTypes.ID_USER_PACKET_ENUM)
            {
                var systemName = Enum.IsDefined(typeof(DefaultMessageIDTypes), messageId)
                    ? ((DefaultMessageIDTypes)messageId).ToString()
                    : "UNKNOWN_SYSTEM_ID";
                Console.WriteLine($"[recv] guid={packet.guid.g:X} raw=0x{messageId:X2} system={systemName} len={packet.length} data: {fullRawHex}");
            }

            if (!_connections.TryGetValue(packet.guid.g, out var connection)
                && messageId == (byte)DefaultMessageIDTypes.ID_NEW_INCOMING_CONNECTION)
            {
                connection = new PlayerConnection(this, new RakNetGUID(packet.guid.g));

                _connections.TryAdd(packet.guid.g, connection);

                Console.WriteLine($"[conn] New connection accepted, guid={packet.guid.g:X}. Total connections: {_connections.Count}");

                goto Deallocate;
            }

            if (connection is null)
            {
                if (messageId >= (byte)DefaultMessageIDTypes.ID_USER_PACKET_ENUM)
                    Console.WriteLine($"[recv] WARNING: user packet 0x{messageId:X2} from guid={packet.guid.g:X} with NO known connection yet (dropped)");
                goto Deallocate;
            }

            if (messageId == (byte)DefaultMessageIDTypes.ID_CONNECTION_LOST ||
                messageId == (byte)DefaultMessageIDTypes.ID_DISCONNECTION_NOTIFICATION)
            {
                if (!_connections.Remove(packet.guid.g, out connection))
                    throw new InvalidOperationException();

                OnConnectionRemoved(connection);

                Console.WriteLine($"[conn] Connection closed, guid={packet.guid.g:X}. Total connections: {_connections.Count}");

                goto Deallocate;
            }

            if (messageId >= (byte)DefaultMessageIDTypes.ID_USER_PACKET_ENUM)
            {
                var rawUserId = messageId - (byte)DefaultMessageIDTypes.ID_USER_PACKET_ENUM;
                var packetId = (PacketId)rawUserId;

                bool handled;
                try
                {
                    handled = connection.ProcessPacket(packetId, bitStream);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[recv] EXCEPTION handling raw=0x{rawUserId:X2} ({packetId}) len={packet.length} data: {fullRawHex}: {ex}");
                    handled = true;
                }

                if (!handled)
                {
                    var totalBits = packet.length * 8;
                    var consumedBits = messageIdWasExtended ? 16 : 8;
                    var remainingBits = (uint)Math.Max(0, totalBits - consumedBits);
                    var remainingBytes = (int)((remainingBits + 7) / 8);
                    var bytesRead = 0;
                    var readBytes = new List<byte>();
                    for (; bytesRead < remainingBytes; bytesRead++)
                    {
                        var oneByte = new byte[1];
                        if (!bitStream.ReadBits(oneByte, 8, alignBitsToRight: true))
                            break;
                        readBytes.Add(oneByte[0]);
                    }
                    var readOk = bytesRead == remainingBytes;
                    var hex = string.Join(" ", readBytes.Select(b => b.ToString("x2")));

                    Console.WriteLine($"[recv] UNHANDLED user packet raw=0x{rawUserId:X2} messageId={messageId} "
                        + $"({packetId}) len={packet.length} guid={packet.guid.g:X} readOk={readOk} bytesRead={bytesRead}/{remainingBytes} data: {hex} full: {fullRawHex}");
                }
                else
                {
                    Console.WriteLine($"[recv] handled raw=0x{rawUserId:X2} ({packetId}) len={packet.length} guid={packet.guid.g:X} data: {fullRawHex}");
                }
            }

        Deallocate:
            _peer.DeallocatePacket(packet);
        }
    }

    public void Send(BitStream bitStream, AddressOrGUID systemIdentifier)
    {
        _peer.Send(bitStream, PacketPriority.HIGH_PRIORITY, PacketReliability.RELIABLE_ORDERED, (char)0, systemIdentifier, false);
    }

    private void OnConnectionRemoved(PlayerConnection connection)
    {
        _connections.Remove(connection.Guid.g);
        connection.OnDisconnected();
    }

    public void Dispose()
    {
        Worlds.Dispose();
        RakPeerInterface.DestroyInstance(_peer);
    }
}