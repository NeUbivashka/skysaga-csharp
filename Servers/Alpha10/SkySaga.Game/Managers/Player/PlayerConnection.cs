using SkySaga.Game.Database;
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
    public PlayerInitializer PlayerInitializer => _playerInitializer;

    public int? LeftHandBlockId { get; set; }
    public int? RightHandBlockId { get; set; }

    public DateTime LastFallRespawn { get; set; }

    public int? LastUsedItemId { get; set; }

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

        if (IsPlayerInitialized)
            GameEvents.Record("player.leave", PlayerSaveStore.CharacterId);

        var removedItems = 0;

        if (PlayerEntity.TryGetComponent<ClientInventoryComponent>(out var inventory))
        {
            if (inventory.VaultInventoryEntity != 0)
            {
                _worldManager.EntityManager.RemoveEntity(inventory.VaultInventoryEntity);

                _playerConnectionManager.BroadcastToAll(new EntityRemoved { Id = inventory.VaultInventoryEntity });
            }

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

    public bool IsPlayerInitialized { get; set; }

    public void SaveState()
    {
        if (!IsPlayerInitialized)
            return;

        var data = _playerInitializer.CaptureSaveData(PlayerEntity);

        data.YawDegrees = LookYaw;
        data.LookPitch = LookPitch;

        PlayerSaveStore.Save(data);
    }

    public bool ProcessPacket(PacketId packetId, BitStream bitStream)
    {
        return packetId switch
        {
            PacketId.QuitGame => QuitGame.Handle(this, bitStream),
            PacketId.ResourceCheck => ResourceCheck.Handle(this, bitStream),
            PacketId.ClientConnected => ClientConnected.Handle(this, bitStream),
            PacketId.ClientReadyToSync => ClientReadyToSync.Handle(this, bitStream),
            PacketId.ClientReadyToPlay => ClientReadyToPlay.Handle(this, bitStream),
            PacketId.ClientInitialSyncFinished => ClientInitialSyncFinished.Handle(this, bitStream),
            PacketId.InventoryItemSwap => InventoryItemSwap.Handle(this, bitStream),
            PacketId.ExecuteEntityAction => ExecuteEntityAction.Handle(this, bitStream),
            PacketId.EndInteractionWithEntity => EndInteractionWithEntity.Handle(this, bitStream),
            PacketId.ActivateJobChallenge => ActivateJobChallenge.Handle(this, bitStream),
            PacketId.TodoListSetTaskTimestamp => TodoListSetTaskTimestamp.Handle(this, bitStream),
            PacketId.MarkChallengeComplete => MarkChallengeComplete.Handle(this, bitStream),
            PacketId.TodoListTaskRemove => TodoListTaskRemove.Handle(this, bitStream),
            PacketId.InventoryArrange => InventoryArrange.Handle(this, bitStream),
            PacketId.InventorySort => InventorySort.Handle(this, bitStream),
            PacketId.InventoryCompact => InventoryCompact.Handle(this, bitStream),
            PacketId.UISettingsSetMiniMapSettings => UISettingsSetMiniMapSettings.Handle(this, bitStream),
            PacketId.SetEmoteBinding => SetEmoteBinding.Handle(this, bitStream),
            PacketId.SetCurrentEmote => SetCurrentEmote.Handle(this, bitStream),
            PacketId.SetPlayerMentalState => SetPlayerMentalState.Handle(this, bitStream),
            PacketId.InventoryItemDrop => InventoryItemDrop.Handle(this, bitStream),
            PacketId.InventoryItemDestroy => InventoryItemDestroy.Handle(this, bitStream),
            PacketId.InventoryItemTransfer => InventoryItemTransfer.Handle(this, bitStream),
            PacketId.InventoryItemTransferToSlot => InventoryItemTransfer.HandleToSlot(this, bitStream),
            PacketId.ShopPurchaseWithCurrency => ShopPurchaseWithCurrency.Handle(this, bitStream),
            PacketId.QueueRecipeOnEntity => QueueRecipeOnEntity.Handle(this, bitStream),
            PacketId.CollectCraftedItemInSlot => CollectCraftedItemInSlot.Handle(this, bitStream),

            PacketId.EntityMoved => EntityMoved.Handle(this, bitStream),
            PacketId.SetInputMovement => SetInputMovement.Handle(this, bitStream),
            PacketId.SetLookAtDirection => SetLookAtDirection.Handle(this, bitStream),
            PacketId.SetPlayerState => SetPlayerState.Handle(this, bitStream),
            PacketId.PlayerDodged => PlayerDodged.Handle(this, bitStream),
            PacketId.PerformJump => PerformJump.Handle(this, bitStream),
            PacketId.EntityLanded => Packets.EntityLanded.Handle(this, bitStream),
            PacketId.RequestRespawn => Packets.RequestRespawn.Handle(this, bitStream),
            PacketId.JoinDestinationSelectionWorld => JoinDestinationSelectionWorld.Handle(this, bitStream),
            PacketId.UISettingsSetUISettings => UISettingsSetUISettings.Handle(this, bitStream),
            PacketId.UISettingsSetControlSettings => UISettingsSetControlSettings.Handle(this, bitStream),
            PacketId.UISettingsSetGameplayCameraState => UISettingsSetGameplayCameraState.Handle(this, bitStream),
            PacketId.UISettingsSetHotbarSlotItemSpec => UISettingsSetHotbarSlotItemSpec.Handle(this, bitStream),
            PacketId.UISettingsSelectHotbarSlot => UISettingsSelectHotbarSlot.Handle(this, bitStream),
            PacketId.RunActionList => RunActionListReceived.Handle(this, bitStream),
            PacketId.AssignCollectionResourcePickup => AssignCollectionResourcePickup.Handle(this, bitStream),
            PacketId.FinalizeCollectionResourcePickup => FinalizeCollectionResourcePickup.Handle(this, bitStream),
            PacketId.EntityPerformedAbility => EntityPerformedAbilityReceived.Handle(this, bitStream),
            PacketId.SetCharacterCustomisationData => SetCharacterCustomisationData.Handle(this, bitStream),
            PacketId.SaveCharacterName => SaveCharacterName.Handle(this, bitStream),
            PacketId.RequestEquipInventoryItem => RequestEquipInventoryItem.Handle(this, bitStream),
            PacketId.RequestUnEquipInventoryItem => RequestUnEquipInventoryItem.Handle(this, bitStream),
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

    public Queue<DateTime> IsLocalDueUtc { get; } = new();

    public byte[]? CustomisationPayload { get; set; }

    public float LookPitch { get; set; }
    public float LookYaw { get; set; }

    public void Tick()
    {
        JobChallenges.RefreshIfDue(this);
        JobChallengeProgress.InventoryChanged(this);

        if (PlayerEntity.TryGetComponent<ClientHealthComponent>(out var health))
        {
            if (health.IsDead && (DateTime.UtcNow - health.DiedUtc).TotalSeconds >= RespawnDelaySeconds)
            {
                GameEvents.Record("player.death", PlayerSaveStore.CharacterId);
                Respawn();
            }

            health.Regenerate(DateTime.UtcNow);
        }

        if (PlayerEntity.TryGetComponent<ClientChargeBarComponent>(out var chargeBar))
            chargeBar.Decay(DateTime.UtcNow);

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

    private const double RespawnDelaySeconds = 10;

    public void Respawn()
    {
        if (PlayerEntity.TryGetComponent<ClientHealthComponent>(out var health))
            health.Revive();

        var (position, yawDegrees) = WorldManager.GetRespawnPoint(CharacterId);

        Teleport(position, yawDegrees);

        Console.WriteLine($"[PlayerConnection] Respawned player {PlayerEntity.Id} at {position}.");
    }

    public Guid? CharacterId => PlayerEntity.TryGetComponent<ClientOwnerComponent>(out var owner) ? owner.Owner : null;

    public void Teleport(Vector3 position, float yawDegrees)
    {
        if (PlayerEntity.TryGetComponent<SmoothedTransformComponent>(out var transform))
        {
            transform.Position = position;
            transform.YawDegrees = yawDegrees;
        }

        Send(new PlayerSpawned
        {
            EntityId = PlayerEntity.Id,
            Position = position,
            YawDegrees = yawDegrees
        });
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