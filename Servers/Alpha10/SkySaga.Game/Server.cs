using RakNet;
using SkySaga.Game.Managers.Player;
using System.Collections.Frozen;
using System.Linq;

namespace SkySaga.Game;

public class Server : IDisposable
{
    private const int MaxConnections = 100;

    private readonly ushort _port;
    private readonly string _password;
    private readonly RakPeerInterface _peer;
    private readonly IWorldManager _worldManager;
    private readonly PlayerInitializer _playerInitializer;
    private readonly PlayerConnectionManager _playerConnectionManager;

    private readonly Dictionary<ulong, PlayerConnection> _connections = [];
    private readonly AiManager _aiManager;
    private readonly ChatCommands _chatCommands;
    private readonly System.Collections.Concurrent.ConcurrentQueue<(string Text, TaskCompletionSource<string?> Reply)> _commands = new();

    public Server(IWorldManager worldManager, PlayerInitializer playerInitializer, PlayerConnectionManager playerConnectionManager, string password = "Something about penguins\0", ushort port = 42069)
    {
        _worldManager = worldManager ?? throw new ArgumentNullException(nameof(worldManager));
        _playerInitializer = playerInitializer ?? throw new ArgumentNullException(nameof(playerInitializer));
        _playerConnectionManager = playerConnectionManager ?? throw new ArgumentNullException(nameof(playerConnectionManager));
        _password = password ?? throw new ArgumentNullException(nameof(password));
        _port = port;

        _aiManager = new AiManager(_worldManager, _playerConnectionManager);
        _chatCommands = new ChatCommands(_worldManager, _playerConnectionManager);

        _peer = RakPeerInterface.GetInstance();

        _peer.SetIncomingPassword(_password, _password.Length);
        _peer.SetMaximumIncomingConnections(MaxConnections);
    }

    public bool Start()
    {
        var status = _peer.Startup(MaxConnections, new SocketDescriptor(_port, string.Empty), 1);

        return status == StartupResult.RAKNET_STARTED;
    }

    public void ResetWorld()
    {
        _resetRequested = true;
    }

    private volatile bool _resetRequested;
    private volatile bool _listChickensRequested;

    private static readonly TimeSpan AutosaveInterval = TimeSpan.FromSeconds(15);
    private DateTime _lastAutosaveUtc = DateTime.UtcNow;

    public void Tick()
    {
        if (_resetRequested)
        {
            _resetRequested = false;
            _worldManager.Reset();
            return;
        }

        if (_listChickensRequested)
        {
            _listChickensRequested = false;
            ListChickens();
        }

        ProcessPackets();
        ProcessCommands();

        _aiManager.Tick();

        ProcessMaps();
        ProcessConnections();
        Autosave();
    }

    public Task<string?> RunCommandAsync(string nickname, string text)
    {
        var reply = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        _commands.Enqueue((text, reply));

        return reply.Task;
    }

    private void ProcessCommands()
    {
        while (_commands.TryDequeue(out var command))
        {
            try
            {
                var connection = _connections.Values.FirstOrDefault(c => c.IsPlayerInitialized);

                command.Reply.SetResult(connection is null ? "No player in the world." : _chatCommands.Execute(connection, command.Text));
            }
            catch (Exception exception)
            {
                command.Reply.SetResult($"Error: {exception.Message}");
            }
        }
    }

    private void Autosave()
    {
        var now = DateTime.UtcNow;

        if (now - _lastAutosaveUtc < AutosaveInterval)
            return;

        _lastAutosaveUtc = now;

        foreach (var connection in _playerConnectionManager.GetAllConnections())
            connection.SaveState();
    }

    public void RequestListChickens()
    {
        _listChickensRequested = true;
    }

    private void ListChickens()
    {
        Console.WriteLine("--- Chickens ---");

        int count = 0;

        foreach (var entity in _worldManager.EntityManager.Entities)
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
                connection = new PlayerConnection(this, packet.guid, packet.systemAddress?.ToString(false), _worldManager, _playerInitializer, _playerConnectionManager);

                _connections.TryAdd(packet.guid.g, connection);

                OnConnectionAdded(connection);

                Console.WriteLine($"[conn] New connection accepted, peer={packet.systemAddress?.ToString(false)}, guid={packet.guid.g:X}, entity={connection.PlayerEntity.Id}, "
                    + $"character={connection.Identity.CharacterUuid}, name={connection.Identity.Name}, save={connection.SaveFileName}. Total connections: {_connections.Count}");

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

    private void ProcessMaps()
    {
        var entities = _worldManager.EntityManager.Entities;

        foreach (var entity in entities)
        {
            if (!entity.SyncRequired)
                continue;

            var entitySync = new EntitySync
            {
                Id = entity.Id,
                SyncData = entity.GetSyncData(newEntity: false)
            };

            _playerConnectionManager.BroadcastToAll(entitySync);
        }

        _playerConnectionManager.FlushRemovedAfterSync();
    }

    private void ProcessConnections()
    {
        foreach (var connection in _connections.ToFrozenSet())
        {
            connection.Value.Tick();
        }
    }

    public void Send(BitStream bitStream, AddressOrGUID systemIdentifier)
    {
        _peer.Send(bitStream, PacketPriority.HIGH_PRIORITY, PacketReliability.RELIABLE_ORDERED, (char)0, systemIdentifier, false);
    }

    private void OnConnectionAdded(PlayerConnection connection)
    {
        _playerConnectionManager.AddConnection(connection.Guid.g, connection);
        connection.OnConnected();
    }

    private void OnConnectionRemoved(PlayerConnection connection)
    {
        _playerConnectionManager.RemoveConnection(connection.Guid.g);

        _connections.Remove(connection.Guid.g);
        connection.OnDisconnected();
    }

    public void Dispose()
    {
        RakPeerInterface.DestroyInstance(_peer);
    }
}
