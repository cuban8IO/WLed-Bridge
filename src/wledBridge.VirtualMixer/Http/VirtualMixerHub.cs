using Microsoft.AspNetCore.SignalR;
using wledBridge.VirtualMixer.Models;
using wledBridge.VirtualMixer.Services;
using wledBridge.VirtualMixer.State;

namespace wledBridge.VirtualMixer.Http;

/// <summary>
/// Bidirectional remote-control hub. Client-to-server methods delegate 1:1 to
/// <see cref="IVirtualMixerRuntime"/> (same thread-safe command pipeline as UI and REST).
/// Server-to-client: "ControlEvent" (ControlEventDto) broadcast by the event bridge.
/// Only reachable if the host explicitly calls MapVirtualMixerApi().
/// </summary>
public sealed class VirtualMixerHub(IVirtualMixerRuntime runtime) : Hub
{
    public Task SetControlValue(Guid controlId, int value) =>
        runtime.SetControlValueAsync(controlId, value, CommandSource.Network);

    public Task SetButtonState(Guid controlId, bool isOn) =>
        runtime.SetButtonStateAsync(controlId, isOn, CommandSource.Network);

    public Task SetLed(Guid controlId, int brightness, string? colorHex) =>
        runtime.SetLedAsync(controlId, new LedState(brightness, colorHex), CommandSource.Network);

    public Task SetSubValue(Guid controlId, int subIndex, int value) =>
        runtime.SetSubValueAsync(controlId, subIndex, value, CommandSource.Network);
}
