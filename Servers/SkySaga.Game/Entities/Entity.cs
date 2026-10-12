namespace SkySaga.Game.Entities;

public class Entity
{
    private readonly BitArray _sync;
    private readonly EntityData _entityData;

    private readonly Dictionary<string, Component> _components = new(StringComparer.OrdinalIgnoreCase);

    public int Id { get; }

    public string Name => _entityData.Name;

    public bool SyncRequired => _sync.HasAnySet();

    /// <summary>
    /// SKYSAGA_SYNC_TRACE=1 logs every parameter written into sync data with its width, so each one
    /// can be checked against the client's binding reader. One wrong width shifts every later
    /// parameter of the entity.
    /// </summary>
    private static readonly bool SyncTrace = Environment.GetEnvironmentVariable("SKYSAGA_SYNC_TRACE") == "1";

    public Entity(int id, EntityData entityData, IReadOnlyCollection<Component> components)
    {
        Id = id;

        _entityData = entityData;

        foreach (var component in components)
        {
            component.ParameterChanged += OnComponentParameterChanged;

            _components.Add(component.Name, component);
        }

        _sync = new BitArray(_entityData.SyncedParametersCount);
    }

    private void OnComponentParameterChanged(Component component, string parameter)
    {
        var syncIndex = _entityData.GetParameterSyncIndex(component.Name, parameter);

        if (syncIndex >= 0)
            _sync.Set(syncIndex, true);
    }

    public bool TryGetComponent<T>([NotNullWhen(true)] out T? component) where T : Component
    {
        component = null;

        if (!_components.TryGetValue(typeof(T).Name, out var baseComponent))
            return false;

        if (baseComponent is not T tempComponent)
            return false;

        component = tempComponent;

        return true;
    }

    /// <param name="forOwner">
    /// True when the recipient is the client that controls this entity. Only that client may see
    /// Player.islocal = true - see PlayerAspectsComponent.IsLocal.
    /// </param>
    /// <param name="onlyParameter">
    /// Sync just this one parameter, regardless of dirty state, and leave the dirty bits alone.
    /// For per-recipient pushes that must not swallow changes meant for every client.
    /// </param>
    public BitStream GetSyncData(bool newEntity, bool forOwner = false, string? onlyParameter = null)
    {
        var bitArray = new BitArray(_entityData.SyncedParametersCount);

        var parameterBitStream = new BitStream();

        foreach (var component in _components.Values)
            component.SyncingForOwner = forOwner;

        for (var i = 0; i < _entityData.SyncedParametersCount; i++)
        {
            if (onlyParameter is null && !newEntity && !_sync[i])
                continue;

            if (!_entityData.TryGetSyncedParameterInfo(i, out var componentName, out var parameterName))
                continue;

            if (onlyParameter is not null && !parameterName.Equals(onlyParameter, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!_components.TryGetValue(componentName, out var component))
                continue;

            var bitsBefore = parameterBitStream.GetNumberOfBitsUsed();

            if (component.TrySync(parameterName, parameterBitStream))
            {
                bitArray.Set(i, true);

                if (SyncTrace)
                    Console.WriteLine($"[SyncTrace] {Name}#{Id} [{i}] {componentName}.{parameterName} "
                        + $"{parameterBitStream.GetNumberOfBitsUsed() - bitsBefore} bits");
            }
        }

        foreach (var component in _components.Values)
            component.SyncingForOwner = false;

        var syncData = new BitStream();

        // Sized from the actual parameter count, rounded up to whole 4-byte words. This used to be
        // a fixed byte[16], i.e. a hard cap of 128 synced parameters - fine for the old
        // Entities.json where Player had 89, but the definitions extracted from this client build
        // give Player 169, and CopyTo would have thrown the moment that file loaded.
        var wordCount = (bitArray.Count + 31) / 32;

        var data = new byte[wordCount * 4];

        bitArray.CopyTo(data, 0);

        // The client's mask reader (FUN_00B44AD0) reads each full 32-bit word and byte-reverses it,
        // so full words go big-endian. The tail (Count % 32 bits, 9 for Player) is read right-aligned
        // into a little-endian int and swapped only on a big-endian host (FUN_00EA76A0 is an ntohl
        // test), so never on x86: the tail stays in CopyTo's natural order. Reversing it too, as this
        // used to, put parameters 160..168 outside the bits the client reads, so they never synced.
        for (int i = 0; i < bitArray.Count / 32; i++)
            Array.Reverse(data, i * 4, 4);

        syncData.WriteBits(data, (uint)bitArray.Count, true);

        // The parameter block length is read by FUN_007D7490, the same reader as the outer length in
        // EntityAdd/EntitySync: an escape bit (set = 32-bit length, clear = 18-bit length), then the
        // length. The escape bit was missing here. The client took the top bit of our length as the
        // escape, folded the first parameter bit into the length, and read every parameter one bit
        // late, for every entity in every EntityAdd and EntitySync. A lone "islocal" bool was read
        // from whatever followed the packet's last bit. That is why it was accepted "sometimes" and
        // then cleared by an identical copy (measured with Tools/trace/trace_islocal.py).
        var parameterBits = parameterBitStream.GetNumberOfBitsUsed();

        if (parameterBits <= 0x20000)
        {
            syncData.Write0();
            syncData.WriteBits(BitConverter.GetBytes(parameterBits), 32 - Util.NumBitsRequiredUInt32(0x20000), true);
        }
        else
        {
            syncData.Write1();
            syncData.WriteBits(BitConverter.GetBytes(parameterBits), 32, true);
        }

        if (parameterBits > 0)
            syncData.Write(parameterBitStream);

        if (onlyParameter is null)
            _sync.SetAll(false);

        return syncData;
    }
}