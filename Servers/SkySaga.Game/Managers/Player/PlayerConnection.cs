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

    /// <summary>
    /// Tears down everything this connection created, not just the player entity.
    ///
    /// A teleport makes the client disconnect and come straight back - that is the protocol, not a
    /// fault - so this runs on every world transition, and anything it leaves behind accumulates.
    /// It used to remove only PlayerEntity, while PlayerInitializer creates a fresh
    /// BasicInventoryItem entity per occupied slot on every connection. One reconnect and the
    /// world held two players' worth of items:
    ///
    ///   connection 1: player Id=4,  items Id=5..18
    ///   connection 2: player Id=19, items Id=5..18 resent, plus new items Id=20..33
    ///
    /// The second client was being sent 28 items, half of them belonging to a player entity it
    /// never received - Id=4 was already gone by then, so those items had a dangling owner.
    ///
    /// The items are reachable from the player's own inventory component, so they come out with
    /// it. Removal order is items first, then the player, so nothing is ever left referring to a
    /// player that no longer exists.
    /// </summary>
    public void OnDisconnected()
    {
        SaveState();

        var removedItems = 0;

        if (PlayerEntity.TryGetComponent<ClientInventoryComponent>(out var inventory))
        {
            foreach (var itemId in inventory.InventoryEntityList)
            {
                if (itemId == 0)
                    continue;

                _worldManager.EntityManager.RemoveEntity(itemId);

                _playerConnectionManager.BroadcastToAll(new EntityRemoved { Id = itemId });

                removedItems++;
            }
        }

        _worldManager.EntityManager.RemoveEntity(PlayerEntity);

        var entityRemoved = new EntityRemoved
        {
            Id = PlayerEntity.Id
        };

        _playerConnectionManager.BroadcastToAll(entityRemoved);

        Console.WriteLine($"[PlayerConnection] Disconnected guid={Guid.g}: removed player entity "
            + $"{PlayerEntity.Id} and {removedItems} inventory item(s).");
    }





    public void SaveState()
    {
        PlayerSaveStore.Save(_playerInitializer.CaptureSaveData(PlayerEntity));
    }

    public bool ProcessPacket(PacketId packetId, BitStream bitStream)
    {
        return packetId switch
        {
            PacketId.QuitGame => QuitGame.Handle(this, bitStream),
            PacketId.ResourceCheck => ResourceCheckReceived.Handle(this, bitStream),
            PacketId.ClientConnected => ClientConnected.Handle(this, bitStream),
            PacketId.ClientReadyToSync => ClientReadyToSync.Handle(this, bitStream),
            PacketId.ClientReadyToPlay => ClientReadyToPlay.Handle(this, bitStream),
            PacketId.ClientInitialSyncFinished => ClientInitialSyncFinished.Handle(this, bitStream),
            PacketId.InventoryItemSwap => InventoryItemSwap.Handle(this, bitStream),
            PacketId.ExecuteEntityAction => ExecuteEntityAction.Handle(this, bitStream),





            PacketId.EntityMoved => EntityMoved.Handle(this, bitStream),
            PacketId.SetLookAtDirection => SetLookAtDirection.Handle(this, bitStream),
            PacketId.RunActionList => RunActionListReceived.Handle(this, bitStream),
            PacketId.SetCharacterCustomisationData => SetCharacterCustomisationData.Handle(this, bitStream),
            PacketId.SaveCharacterName => SaveCharacterName.Handle(this, bitStream),
            PacketId.RequestEquipInventoryItem => RequestEquipInventoryItem.Handle(this, bitStream),
            PacketId.EnteredGameplay => EnteredGameplay.Handle(this, bitStream),
            PacketId.FullScreenVideoCompleted => FullScreenVideoCompleted.Handle(this, bitStream),
            PacketId.RequestStartAnimationSequence => RequestStartAnimationSequenceReceived.Handle(this, bitStream),
            PacketId.AnimationSequenceCompleted => AnimationSequenceCompletedReceived.Handle(this, bitStream),
            PacketId.NarrativeCompleted => NarrativeCompletedReceived.Handle(this, bitStream),





            (PacketId)241 => PerformVoxelActions.Handle(this, bitStream),
            (PacketId)242 => RequestChatChannelData.Handle(this, bitStream),
            _ => false
        };
    }

    /// <summary>
    /// When to push Player.islocal = true to this client (see EnteredGameplay.ScheduleIsLocal).
    /// Sent from Tick, i.e. on the server thread: it used to go out from a Task.Delay continuation,
    /// which raced ProcessMaps over Component.SyncingForOwner and sent islocal = false roughly
    /// every other time - the client applied the bit, then the next copy cleared it again.
    /// </summary>
    public Queue<DateTime> IsLocalDueUtc { get; } = new();

    /// <summary>
    /// The latest CustomisationData struct from the character creator (raw bits, not decoded yet).
    /// Applied to the Player at SaveCharacterName.
    /// </summary>
    public byte[]? CustomisationPayload { get; set; }

    public void Tick()
    {
        if (IsLocalDueUtc.TryPeek(out var due) && DateTime.UtcNow >= due)
        {
            IsLocalDueUtc.Dequeue();

            Send(new EntitySync
            {
                Id = PlayerEntity.Id,
                SyncData = PlayerEntity.GetSyncData(newEntity: false, forOwner: true, onlyParameter: "islocal")
            });

            Console.WriteLine($"[PlayerConnection] Sent EntitySync(EntityId={PlayerEntity.Id}, islocal=true) to the owner.");
        }
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