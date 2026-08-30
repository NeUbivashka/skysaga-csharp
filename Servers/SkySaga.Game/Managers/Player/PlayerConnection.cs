using SkySaga.Game.Packets;

namespace SkySaga.Game.Managers.Player;

public class PlayerConnection
{
    private readonly Server _server;
    private readonly IWorldManager _worldManager;
    private readonly PlayerInitializer _playerInitializer;
    private readonly PlayerConnectionManager _playerConnectionManager;

    public readonly RakNetGUID Guid;

    public Entity PlayerEntity { get; }
    public IWorldManager WorldManager => _worldManager;
    public PlayerConnectionManager PlayerConnectionManager => _playerConnectionManager;

    public int? LeftHandBlockId { get; set; }
    public int? RightHandBlockId { get; set; }





    public string? CurrentVideoFileName { get; set; }

    public PlayerConnection(Server server, RakNetGUID guid, IWorldManager worldManager, PlayerInitializer playerInitializer, PlayerConnectionManager playerConnectionManager)
    {
        _server = server ?? throw new ArgumentNullException(nameof(server));
        _worldManager = worldManager ?? throw new ArgumentNullException(nameof(worldManager));
        _playerInitializer = playerInitializer ?? throw new ArgumentNullException(nameof(playerInitializer));
        _playerConnectionManager = playerConnectionManager ?? throw new ArgumentNullException(nameof(playerConnectionManager));

        Guid = guid;

        _worldManager.EntityManager.TryCreateEntity("Player", out var player);

        ArgumentNullException.ThrowIfNull(player);

        PlayerEntity = player;
    }

    public void OnConnected()
    {
    }

    public void OnDisconnected()
    {


        SaveState();

        _worldManager.EntityManager.RemoveEntity(PlayerEntity);

        var entityRemoved = new EntityRemoved
        {
            Id = PlayerEntity.Id
        };

        _playerConnectionManager.BroadcastToAll(entityRemoved);
    }





    public void SaveState()
    {
        PlayerSaveStore.Save(_playerInitializer.CaptureSaveData(PlayerEntity));
    }

    public bool ProcessPacket(PacketId packetId, BitStream bitStream)
    {
        return packetId switch
        {
            PacketId.ClientConnected => ClientConnected.Handle(this, bitStream),
            PacketId.ClientReadyToSync => ClientReadyToSync.Handle(this, bitStream),
            PacketId.ClientReadyToPlay => ClientReadyToPlay.Handle(this, bitStream),
            PacketId.ClientInitialSyncFinished => ClientInitialSyncFinished.Handle(this, bitStream),
            PacketId.InventoryItemSwap => InventoryItemSwap.Handle(this, bitStream),
            PacketId.ExecuteEntityAction => ExecuteEntityAction.Handle(this, bitStream),





            PacketId.EntityMoved => EntityMoved.Handle(this, bitStream),
            PacketId.SetLookAtDirection => SetLookAtDirection.Handle(this, bitStream),
            PacketId.RunActionList => RunActionListReceived.Handle(this, bitStream),
            PacketId.RequestEquipInventoryItem => RequestEquipInventoryItem.Handle(this, bitStream),
            PacketId.EnteredGameplay => EnteredGameplay.Handle(this, bitStream),
            PacketId.FullScreenVideoCompleted => FullScreenVideoCompleted.Handle(this, bitStream),





            (PacketId)241 => PerformVoxelActions.Handle(this, bitStream),
            (PacketId)242 => RequestChatChannelData.Handle(this, bitStream),
            _ => false
        };
    }

    public void Tick()
    {
    }

    public void Send(BitStream bitStream)
    {
        _server.Send(bitStream, Guid);
    }

    public void Send(ISerializablePacket packet)
    {
        _server.Send(packet.Serialize(), Guid);
    }

    public void InitialChunkSync()
    {
        _worldManager.SendInitialChunks(this);
    }

    public void InitialEntitiySync()
    {
        _worldManager.SendInitialEntities(this, PlayerEntity, _playerInitializer);
    }
}