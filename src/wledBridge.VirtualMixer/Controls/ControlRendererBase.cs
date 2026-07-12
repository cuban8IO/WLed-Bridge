using Microsoft.AspNetCore.Components;
using wledBridge.VirtualMixer.Models;
using wledBridge.VirtualMixer.State;

namespace wledBridge.VirtualMixer.Controls;

/// <summary>
/// Base for all control renderer components (used identically by designer and runtime).
/// Subscribes to per-control state notifications for render isolation: only the affected
/// control re-renders on a value change, never the whole surface.
/// </summary>
public abstract class ControlRendererBase : ComponentBase, IDisposable
{
    private IDisposable? _subscription;

    [Parameter] public required ControlInstance Instance { get; set; }

    /// <summary>True inside the designer's edit mode - interactions are disabled there.</summary>
    [Parameter] public bool EditMode { get; set; }

    [Inject] internal MixerRuntimeState Runtime { get; set; } = default!;

    protected ControlStateSnapshot? State => Runtime.GetControlState(Instance.Id);

    protected bool Interactive => !EditMode && Instance.IsEnabled;

    protected override void OnInitialized()
    {
        _subscription = Runtime.SubscribeControl(Instance.Id, () => _ = InvokeAsync(StateHasChanged));
    }

    protected Task Send(IControlCommand command) =>
        Interactive ? Runtime.SendCommandAsync(command) : Task.CompletedTask;

    public void Dispose()
    {
        _subscription?.Dispose();
        GC.SuppressFinalize(this);
    }
}
