using Microsoft.AspNetCore.Components;
using SkySaga.Admin.Services;

namespace SkySaga.Admin.Components.Shared;

public abstract class LivePage : ComponentBase, IDisposable
{
    [Inject] protected LiveMetrics Metrics { get; set; } = default!;

    protected LiveSnapshot Live => Metrics.Snapshot;

    protected override void OnInitialized() => Metrics.Changed += OnMetricsChanged;

    protected virtual Task OnLiveChangedAsync() => Task.CompletedTask;

    private void OnMetricsChanged() => _ = InvokeAsync(async () =>
    {
        await OnLiveChangedAsync();
        StateHasChanged();
    });

    public virtual void Dispose()
    {
        Metrics.Changed -= OnMetricsChanged;
        GC.SuppressFinalize(this);
    }
}
