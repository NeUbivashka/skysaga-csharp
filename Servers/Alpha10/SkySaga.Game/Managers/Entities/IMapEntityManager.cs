namespace SkySaga.Game.Managers.Entities;

public interface IMapEntityManager
{
    IEnumerable<Entity> Entities { get; }

    bool TryGetEntity(int id, [NotNullWhen(true)] out Entity? entity);

    bool TryCreateEntity(string name, [NotNullWhen(true)] out Entity? entity);

    void RemoveEntity(Entity entity);

    void RemoveEntity(int id);
}
