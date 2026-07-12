using wledBridge.VirtualMixer.State;

namespace wledBridge.VirtualMixer.Events;

/// <summary>
/// Base for all control events raised by the runtime. The module does not know or care what
/// subscribers do with them (MIDI, hardware, network, automation, logging are host concerns).
/// Events are raised on the runtime's consumer thread - subscribers must marshal themselves
/// (Blazor components via InvokeAsync).
/// </summary>
public abstract class ControlEventArgs : EventArgs
{
    public DateTime Timestamp { get; } = DateTime.Now;
    public required Guid MixerId { get; init; }
    public required string MixerName { get; init; }
    public required Guid ControlId { get; init; }
    public required string ControlName { get; init; }
    public required string TypeKey { get; init; }
    public int? SubIndex { get; init; }
    public CommandSource Source { get; init; }

    /// <summary>Display strings for the event monitor's old/new value columns.</summary>
    public abstract string? OldValueText { get; }
    public abstract string? NewValueText { get; }
    public abstract string EventName { get; }
}

public sealed class ControlPressedEventArgs : ControlEventArgs
{
    public override string? OldValueText => null;
    public override string? NewValueText => null;
    public override string EventName => "Pressed";
}

public sealed class ControlReleasedEventArgs : ControlEventArgs
{
    public override string? OldValueText => null;
    public override string? NewValueText => null;
    public override string EventName => "Released";
}

public sealed class ControlValueChangedEventArgs : ControlEventArgs
{
    public required int OldValue { get; init; }
    public required int NewValue { get; init; }

    public override string? OldValueText => OldValue.ToString();
    public override string? NewValueText => NewValue.ToString();
    public override string EventName => "ValueChanged";
}

/// <summary>Toggle-state and LED changes; old/new carried as display strings.</summary>
public sealed class ControlStateChangedEventArgs : ControlEventArgs
{
    public required string OldState { get; init; }
    public required string NewState { get; init; }

    public override string? OldValueText => OldState;
    public override string? NewValueText => NewState;
    public override string EventName => "StateChanged";
}

/// <summary>Relative knob turn (mode Relative) - stateless, delta only.</summary>
public sealed class ControlDeltaChangedEventArgs : ControlEventArgs
{
    public required int Delta { get; init; }

    public override string? OldValueText => null;
    public override string? NewValueText => Delta > 0 ? $"+{Delta}" : Delta.ToString();
    public override string EventName => "DeltaChanged";
}

/// <summary>Raised when a command could not be applied (e.g. SetValue on a relative knob).</summary>
public sealed class ControlCommandRejectedEventArgs : ControlEventArgs
{
    public required string Reason { get; init; }

    public override string? OldValueText => null;
    public override string? NewValueText => Reason;
    public override string EventName => "Rejected";
}
