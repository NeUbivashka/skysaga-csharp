using System.Linq;

namespace SkySaga.Game.Entities;

public class Entity
{
    private readonly BitArray _sync;
    private readonly EntityData _entityData;

    private readonly Dictionary<string, Component> _components = new(StringComparer.OrdinalIgnoreCase);

    public int Id { get; }

    public string Name => _entityData.Name;

    public bool SyncRequired => _sync.HasAnySet();

    public IReadOnlyList<string> TextListParameter(string name)
        => _entityData.Parameters.Find(parameter => parameter.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) is { } parameter
           && parameter.Value.ValueKind == System.Text.Json.JsonValueKind.Array
            ? [.. parameter.Value.EnumerateArray()
                .Where(value => value.ValueKind == System.Text.Json.JsonValueKind.String)
                .Select(value => value.GetString()!)]
            : [];

    public string? TextParameter(string name)
        => _entityData.Parameters.Find(parameter => parameter.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) is { } parameter
           && parameter.Value.ValueKind == System.Text.Json.JsonValueKind.String
            ? parameter.Value.GetString()
            : null;

    public bool? BoolParameter(string name)
        => _entityData.Parameters.Find(parameter => parameter.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) is { } parameter
           && parameter.Value.ValueKind is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False
            ? parameter.Value.GetBoolean()
            : null;

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

        var wordCount = (bitArray.Count + 31) / 32;

        var data = new byte[wordCount * 4];

        bitArray.CopyTo(data, 0);

        for (int i = 0; i < bitArray.Count / 32; i++)
            Array.Reverse(data, i * 4, 4);

        syncData.WriteBits(data, (uint)bitArray.Count, true);

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
