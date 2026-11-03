namespace SkySaga.Game.Components;

public class BreatheComponent : Component
{
    private const int WireScale = 32;
    private const uint WireRange = 2 * WireScale;
    private const double OxygenDepletionSeconds = 30;
    private const double OxygenRecoverySeconds = 3;
    private const double HealthDepletionSeconds = 15;
    private const double SecondsBetweenHealthLoss = 1.5;

    private double _oxygen = 1;
    private double _sinceHealthLoss;
    private DateTime _lastUpdateUtc = DateTime.UtcNow;

    public int OxygenLevel { get; set => SetIfChanged(ref field, value); } = WireScale;

    public void Update(bool underwater, HealthComponent health, DateTime now)
    {
        var elapsed = (now - _lastUpdateUtc).TotalSeconds;
        _lastUpdateUtc = now;

        if (health.IsDead)
        {
            _oxygen = 1;
            _sinceHealthLoss = 0;
            OxygenLevel = WireScale;
            return;
        }

        _oxygen = underwater
            ? Math.Max(_oxygen - elapsed / OxygenDepletionSeconds, 0)
            : Math.Min(_oxygen + elapsed / OxygenRecoverySeconds, 1);
        OxygenLevel = (int)Math.Round(_oxygen * WireScale);

        if (!underwater || _oxygen > 0)
        {
            _sinceHealthLoss = 0;
            return;
        }

        _sinceHealthLoss += elapsed;

        if (_sinceHealthLoss < SecondsBetweenHealthLoss)
            return;

        _sinceHealthLoss -= SecondsBetweenHealthLoss;
        health.Damage((int)Math.Ceiling(health.MaxHP * SecondsBetweenHealthLoss / HealthDepletionSeconds), HealthComponent.DamageReasonDrowned);
    }

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (!parameterName.Equals(nameof(OxygenLevel), StringComparison.OrdinalIgnoreCase))
            return false;

        bitStream.WriteRanged((uint)(OxygenLevel + WireScale), WireRange);
        bitStream.Write(false);

        return true;
    }
}
