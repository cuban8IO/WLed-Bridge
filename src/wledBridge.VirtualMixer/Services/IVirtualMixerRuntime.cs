using wledBridge.VirtualMixer.Events;
using wledBridge.VirtualMixer.Models;
using wledBridge.VirtualMixer.State;

namespace wledBridge.VirtualMixer.Services;

/// <summary>
/// Public runtime API of the module: external control of control properties plus the outgoing
/// event stream. All methods are thread-safe and may be called from any thread (e.g. MIDI
/// callback threads) - commands are marshalled through an internal single-consumer pipeline.
/// </summary>
public interface IVirtualMixerRuntime
{
    /// <summary>Sets the 0..127 value of a fader, absolute knob or statusbar.</summary>
    Task SetControlValueAsync(Guid controlId, int value, CommandSource source = CommandSource.External);

    /// <summary>Sets a button's on/off (toggle) state.</summary>
    Task SetButtonStateAsync(Guid controlId, bool isOn, CommandSource source = CommandSource.External);

    /// <summary>Sets LED brightness (0..127) and, for RGB LEDs, an optional color override.</summary>
    Task SetLedAsync(Guid controlId, LedState led, CommandSource source = CommandSource.External);

    /// <summary>Sets the value of one mini knob inside a knob bank.</summary>
    Task SetSubValueAsync(Guid controlId, int subIndex, int value, CommandSource source = CommandSource.External);

    /// <summary>Typed, extensible fallback: enqueues any control command.</summary>
    Task SendCommandAsync(IControlCommand command);

    /// <summary>Read access to a control's current runtime state (null if unknown).</summary>
    ControlStateSnapshot? GetControlState(Guid controlId);

    /// <summary>All control events: Pressed, Released, ValueChanged, StateChanged, DeltaChanged.</summary>
    event EventHandler<ControlEventArgs>? ControlEvent;
}
