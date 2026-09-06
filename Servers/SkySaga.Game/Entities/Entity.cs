namespace SkySaga.Game.Entities;

public class Entity
{
    private readonly BitArray _sync;
    private readonly EntityData _entityData;

    private readonly Dictionary<string, Component> _components = new(StringComparer.OrdinalIgnoreCase);

    public int Id { get; }

    public string Name => _entityData.Name;

    public bool SyncRequired => _sync.HasAnySet();

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

        if (syncIndex > 0)
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

    public BitStream GetSyncData(bool newEntity)
    {
        var bitArray = new BitArray(_entityData.SyncedParametersCount);

        var parameterBitStream = new BitStream();

        for (var i = 0; i < _entityData.SyncedParametersCount; i++)
        {
            if (!newEntity && !_sync[i])
                continue;

            if (!_entityData.TryGetSyncedParameterInfo(i, out var componentName, out var parameterName))
                continue;

            if (!_components.TryGetValue(componentName, out var component))
                continue;

            if (component.TrySync(parameterName, parameterBitStream))
            {
                bitArray.Set(i, true);

            }
        }

        var syncData = new BitStream();

        // Sized from the actual parameter count, rounded up to whole 4-byte words. This used to be
        // a fixed byte[16], i.e. a hard cap of 128 synced parameters - fine for the old
        // Entities.json where Player had 89, but the definitions extracted from this client build
        // give Player 169, and CopyTo would have thrown the moment that file loaded.
        var wordCount = (bitArray.Count + 31) / 32;

        var data = new byte[wordCount * 4];

        bitArray.CopyTo(data, 0);

        // Also rounded up: with 169 bits, Count / 32 is 5 and the last 9 bits would keep the wrong
        // byte order.
        for (int i = 0; i < wordCount; i++)
            Array.Reverse(data, i * 4, 4);

        syncData.WriteBits(data, (uint)bitArray.Count);

        syncData.WriteBits(BitConverter.GetBytes(parameterBitStream.GetNumberOfBitsUsed()), 32 - Util.NumBitsRequiredUInt32(0x20000));

        if (parameterBitStream.GetNumberOfBitsUsed() > 0)
            syncData.Write(parameterBitStream);

        _sync.SetAll(false);

        return syncData;
    }
}