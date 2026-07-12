using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using wledBridge.VirtualMixer.Events;
using wledBridge.VirtualMixer.Services;

namespace wledBridge.VirtualMixer.Http;

/// <summary>
/// Broadcasts runtime control events to connected SignalR clients. Dormant unless the host has
/// registered SignalR services (AddSignalR): without them, IHubContext resolution returns null
/// and the bridge simply never subscribes - the core stays completely SignalR-agnostic.
/// </summary>
internal sealed class VirtualMixerEventBridge(IServiceProvider services, IVirtualMixerRuntime runtime)
    : IHostedService
{
    private IHubContext<VirtualMixerHub>? _hubContext;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _hubContext = services.GetService<IHubContext<VirtualMixerHub>>();
        if (_hubContext is not null)
        {
            runtime.ControlEvent += OnControlEvent;
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_hubContext is not null)
        {
            runtime.ControlEvent -= OnControlEvent;
        }

        return Task.CompletedTask;
    }

    private void OnControlEvent(object? sender, ControlEventArgs args)
    {
        _ = _hubContext?.Clients.All.SendAsync("ControlEvent", ControlEventDto.From(args));
    }
}
