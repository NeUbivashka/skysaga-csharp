namespace SkySaga.Game.Managers.Entities;

using System.Reflection;

public class EntityManager : IMapEntityManager
{
    private static int _uniqueEntityId = 1;
    private readonly Dictionary<int, Entity> _entities = [];

    private static readonly Dictionary<string, Type> _components = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, EntityData> _entityTemplates = new(StringComparer.OrdinalIgnoreCase);

    static EntityManager()
    {
        LoadEntityTemplates();
        LoadAssemblyComponents();
    }

    private static void LoadAssemblyComponents()
    {
        var componentType = typeof(Component);

        foreach (var assemblyType in componentType.Assembly.GetTypes())
        {
            if (!assemblyType.IsClass
                || assemblyType.IsAbstract
                || !assemblyType.IsSubclassOf(componentType))
                continue;

            _components.Add(assemblyType.Name.ToLower(), assemblyType);
        }
    }

    private static void LoadEntityTemplates()
    {
        using var fileStream = File.OpenRead(@"Data\Entities.json");

        using var jsonDocument = JsonDocument.Parse(fileStream);

        if (!jsonDocument.RootElement.TryGetPropertyIgnoreCase(nameof(Entities), out var entitiesElement) &&
            entitiesElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException();

        foreach (var entityElement in entitiesElement.EnumerateArray())
        {
            if (!entityElement.TryGetPropertyIgnoreCase("Name", out var nameElement) ||
                nameElement.ValueKind != JsonValueKind.String)
                throw new InvalidOperationException();

            var entityName = nameElement.GetString();

            ArgumentException.ThrowIfNullOrWhiteSpace(entityName);

            if (!entityElement.TryGetPropertyIgnoreCase("Parameters", out var parametersElement) ||
                parametersElement.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException();

            List<EntityData.ParameterData> parameters = [];

            foreach (var parameterProperty in parametersElement.EnumerateObject())
            {
                var parameterName = parameterProperty.Name;

                var parameterInfo = new EntityData.ParameterData
                {
                    Name = parameterName
                };

                if (parameterProperty.Value.TryGetPropertyIgnoreCase("SyncIndex", out var syncIndexElement))
                {
                    if (!syncIndexElement.TryGetInt32(out int syncIndex))
                        throw new InvalidOperationException();

                    parameterInfo.SyncIndex = syncIndex;
                }

                if (parameterProperty.Value.TryGetPropertyIgnoreCase("Value", out var valueElement))
                {
                    parameterInfo.Value = valueElement.Clone();
                }

                parameters.Add(parameterInfo);
            }

            if (!entityElement.TryGetPropertyIgnoreCase("Client", out var clientElement) ||
                clientElement.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException();

            if (!clientElement.TryGetPropertyIgnoreCase("Components", out var componentsElement) ||
                componentsElement.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException();

            List<EntityData.ComponentData> components = [];

            foreach (var componentProperty in componentsElement.EnumerateObject())
            {
                var componentName = componentProperty.Name;

                var componentInfo = new EntityData.ComponentData
                {
                    Name = componentName
                };

                if (!componentProperty.Value.TryGetPropertyIgnoreCase("Bindings", out var bindingsElement))
                    continue;

                if (bindingsElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var bindingProperty in bindingsElement.EnumerateObject())
                    {
                        var bindingName = bindingProperty.Name;

                        if (!bindingProperty.Value.TryGetPropertyIgnoreCase("MapsTo", out var mapsToElement) ||
                            mapsToElement.ValueKind != JsonValueKind.String)
                            throw new InvalidOperationException();

                        var mapsTo = mapsToElement.GetString();

                        ArgumentException.ThrowIfNullOrWhiteSpace(mapsTo);

                        componentInfo.Bindings.TryAdd(bindingName, mapsTo);
                    }
                }
                else if (bindingsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var bindingElement in bindingsElement.EnumerateArray())
                    {
                        if (bindingElement.ValueKind != JsonValueKind.String)
                            throw new InvalidOperationException();

                        var bindingName = bindingElement.GetString();

                        ArgumentException.ThrowIfNullOrWhiteSpace(bindingName);

                        componentInfo.Bindings.TryAdd(bindingName, bindingName);
                    }
                }
                else
                {
                    continue;
                }

                components.Add(componentInfo);
            }

            _entityTemplates.Add(entityName, new EntityData(entityName, parameters, components));
        }

        Console.WriteLine($"[EntityManager] Loaded {_entityTemplates.Count} entity templates.");

        if (_entityTemplates.TryGetValue("Player", out var playerTemplate))
            Console.WriteLine($"[EntityManager] Player: {playerTemplate.Parameters.Count} parameters, "
                + $"{playerTemplate.SyncedParametersCount} synced, "
                + $"{playerTemplate.Components.Count} components.");
        else
            Console.WriteLine("[EntityManager] Player template NOT FOUND.");
    }

    public IEnumerable<Entity> Entities => _entities.Values;

    public bool TryGetEntity(int id, [NotNullWhen(true)] out Entity? entity)
    {
        return _entities.TryGetValue(id, out entity);
    }

    public bool TryCreateEntity(int id, string name, [NotNullWhen(true)] out Entity? entity)
    {
        if (!_entityTemplates.TryGetValue(name, out var entityData))
        {
            entity = null;
            return false;
        }

        List<Component> components = new(entityData.Components.Count);

        foreach (var componentData in entityData.Components)
        {
            if (!_components.TryGetValue(componentData.Name, out var componentType))
            {
                Debug.WriteLine(componentData.Name, "Unimplemented Component");
                continue;
            }

            var component = (Component?)Activator.CreateInstance(componentType);

            ArgumentNullException.ThrowIfNull(component);

            ApplyDefaultParameterValues(component, componentType, componentData, entityData);

            components.Add(component);
        }

        entity = new Entity(id, entityData, components);

        _entities.TryAdd(entity.Id, entity);
        return true;
    }

    private static void ApplyDefaultParameterValues(Component component, Type componentType, EntityData.ComponentData componentData, EntityData entityData)
    {
        foreach (var binding in componentData.Bindings)
        {
            var parameter = entityData.Parameters.Find(p =>
                string.Equals(p.Name, binding.Value, StringComparison.OrdinalIgnoreCase));

            if (parameter is null || parameter.Value.ValueKind == JsonValueKind.Undefined)
                continue;

            var property = componentType.GetProperty(
                binding.Key,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (property is null || !property.CanWrite)
                continue;

            var convertedValue = ConvertJsonElement(parameter.Value, property.PropertyType);

            if (convertedValue is null)
                continue;

            property.SetValue(component, convertedValue);
        }
    }

    private static object? ConvertJsonElement(JsonElement value, Type targetType)
    {
        if (targetType == typeof(string))
            return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();

        if (targetType == typeof(int))
            return value.TryGetInt32(out var intValue) ? intValue : null;

        if (targetType == typeof(float))
        {
            if (value.TryGetSingle(out var floatValue))
                return floatValue;

            return value.TryGetDouble(out var doubleValue) ? (float)doubleValue : null;
        }

        if (targetType == typeof(bool))
        {
            return value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null,
            };
        }

        if (targetType == typeof(byte))
            return value.TryGetByte(out var byteValue) ? byteValue : null;

        if (targetType == typeof(uint?) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } name)
            return (uint?)Util.ComputeCrc32(name);

        return null;
    }

    public bool TryCreateEntity(string name, [NotNullWhen(true)] out Entity? entity)
    {
        return TryCreateEntity(_uniqueEntityId++, name, out entity);
    }

    public void RemoveEntity(Entity entity)
    {
        _entities.Remove(entity.Id);
    }

    public void RemoveEntity(int id)
    {
        _entities.Remove(id);
    }

    public void Clear()
    {
        _entities.Clear();
    }
}
