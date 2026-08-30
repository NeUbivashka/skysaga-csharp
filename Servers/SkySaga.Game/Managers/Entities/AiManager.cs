namespace SkySaga.Game.Managers.Entities;

public class AiManager
{
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromMilliseconds(150);

    private const float WanderRadius = 3f;
    private const float WanderSpeed = 0.5f;
    private const float FleeSpeed = 1.2f;
    private const float FleeTriggerDistance = 5f;
    private const float FleeStopDistance = 9f;
    private const float GroundSnapSpeed = 4f;

    private readonly IWorldManager _worldManager;
    private readonly Random _random = new();
    private readonly Dictionary<int, WanderState> _wanderStates = [];

    private DateTime _lastUpdate = DateTime.UtcNow;

    private class WanderState
    {
        public Vector3 Target;
        public DateTime NextDecisionTime;
    }

    public AiManager(IWorldManager worldManager)
    {
        _worldManager = worldManager ?? throw new ArgumentNullException(nameof(worldManager));
    }

    public void Tick()
    {
        var now = DateTime.UtcNow;

        if (now - _lastUpdate < UpdateInterval)
            return;

        var delta = (float)(now - _lastUpdate).TotalSeconds;
        _lastUpdate = now;

        foreach (var entity in _worldManager.EntityManager.Entities)
        {
            if (!entity.TryGetComponent<ClientEnemyAiComponent>(out var ai))
                continue;




            if (!entity.TryGetComponent<SmoothedTransformComponent>(out var transform))
                continue;

            if (!string.Equals(ai.AiCapabilities, "chicken", StringComparison.OrdinalIgnoreCase))
                continue;

            UpdateChicken(entity, ai, transform, now, delta);
        }
    }

    private void UpdateChicken(Entity entity, ClientEnemyAiComponent ai, TransformComponent transform, DateTime now, float delta)
    {
        if (ai.SpawnPosition == default)
            ai.SpawnPosition = transform.Position;

        var nearestPlayer = FindNearestPlayer(transform.Position, out var playerDistance, out var playerPosition);
        var wasFleeing = ai.StateName == "flee";

        if (nearestPlayer is not null && playerDistance < (wasFleeing ? FleeStopDistance : FleeTriggerDistance))
        {
            if (!wasFleeing)
            {

                Console.WriteLine(
                    $"[AiManager] Chicken {entity.Id} startled - before: {transform.Position}, playerPos: {playerPosition}, playerDistance: {playerDistance:F2}");
            }

            ai.StateName = "flee";
            ai.Mood = "scared";
            ai.TargetEntityId = nearestPlayer.Id;

            var away = transform.Position - playerPosition;
            away.Y = 0f;



            if (away.LengthSquared() < 0.0001f)
                away = new Vector3(1f, 0f, 0f);

            MoveTowards(transform, Vector3.Normalize(away), FleeSpeed * delta);
            SnapToGround(transform, delta);

            if (!wasFleeing)
            {

                Console.WriteLine($"[AiManager] Chicken {entity.Id} startled - after: {transform.Position}");
            }

            return;
        }

        if (wasFleeing)
        {
            ai.StateName = "idle";
            ai.Mood = "calm";
            ai.TargetEntityId = 0;
        }

        if (!_wanderStates.TryGetValue(entity.Id, out var wander) || now >= wander.NextDecisionTime)
        {
            var angle = (float)(_random.NextDouble() * Math.PI * 2);
            var dist = (float)(_random.NextDouble() * WanderRadius);

            wander = new WanderState
            {
                Target = ai.SpawnPosition + new Vector3(MathF.Cos(angle) * dist, 0f, MathF.Sin(angle) * dist),
                NextDecisionTime = now.AddSeconds(3 + _random.NextDouble() * 4)
            };
            _wanderStates[entity.Id] = wander;

            ai.StateName = _random.NextDouble() < 0.35 ? "idle" : "wander";
            ai.Mood = "calm";
        }

        if (ai.StateName != "wander")
            return;

        var toTarget = wander.Target - transform.Position;
        toTarget.Y = 0f;

        if (toTarget.LengthSquared() < 0.09f)
        {
            ai.StateName = "idle";
            return;
        }

        MoveTowards(transform, Vector3.Normalize(toTarget), WanderSpeed * delta);
        SnapToGround(transform, delta);
    }











    private void SnapToGround(TransformComponent transform, float delta)
    {
        int mapHeightVoxels = Math.Max(_worldManager.Definition.MapSizeChunks.Y, 1) * 32;
        int groundY = SpawnFinder.FindGroundHeight(
            _worldManager.ChunkManager,
            (int)MathF.Floor(transform.Position.X),
            (int)MathF.Floor(transform.Position.Z),
            mapHeightVoxels);

        if (groundY < 0)
            return;

        var position = transform.Position;
        float targetY = groundY + 1.1f;
        float maxStep = GroundSnapSpeed * delta;
        float newY = position.Y + Math.Clamp(targetY - position.Y, -maxStep, maxStep);

        transform.Position = new Vector3(position.X, newY, position.Z);
    }

    private static void MoveTowards(TransformComponent transform, Vector3 direction, float distance)
    {
        transform.Position += direction * distance;
        transform.YawDegrees = MathF.Atan2(direction.X, direction.Z) * (180f / MathF.PI);
    }

    private Entity? FindNearestPlayer(Vector3 position, out float distance, out Vector3 playerPosition)
    {
        distance = float.MaxValue;
        playerPosition = default;
        Entity? nearest = null;

        foreach (var entity in _worldManager.EntityManager.Entities)
        {


            if (!entity.TryGetComponent<ClientPlayerAspectsComponent>(out _))
                continue;

            if (!entity.TryGetComponent<SmoothedTransformComponent>(out var transform))
                continue;

            var d = Vector3.Distance(position, transform.Position);

            if (d < distance)
            {
                distance = d;
                nearest = entity;
                playerPosition = transform.Position;
            }
        }

        return nearest;
    }
}
