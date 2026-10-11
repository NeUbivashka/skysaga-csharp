using System.Runtime.CompilerServices;

namespace SkySaga.Game.Components;

public delegate void ParameterChangedEventHandler(Component component, string parameterName);

public abstract class Component
{
    public string Name => GetType().Name;

    public event ParameterChangedEventHandler? ParameterChanged;

    /// <summary>
    /// True while Entity.GetSyncData is building sync data for the client that owns this entity.
    /// Lets per-recipient parameters (Player's "islocal") differ between the owner and everyone else.
    /// </summary>
    public bool SyncingForOwner { get; internal set; }

    protected bool SetIfChanged<T>(ref T field, T value, [CallerMemberName] string? parameterName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;

        ParameterChanged?.Invoke(this, parameterName ?? string.Empty);

        return true;
    }

    public abstract bool TrySync(string parameterName, BitStream bitStream);
}