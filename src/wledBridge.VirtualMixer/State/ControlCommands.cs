using wledBridge.VirtualMixer.Models;

namespace wledBridge.VirtualMixer.State;

/// <summary>Origin of a state change, carried through to events (visible in the event monitor).</summary>
public enum CommandSource
{
    Ui,
    External,
    Binding,
    Network,
    Simulation
}

/// <summary>
/// A typed state-change command. All mutations - UI interaction, external API, bindings,
/// network - travel through the same single-consumer command pipeline, which is what makes the
/// runtime thread-safe. Extensible: custom control types can define their own commands and
/// handle them in their state handling.
/// </summary>
public interface IControlCommand
{
    Guid ControlId { get; }
    CommandSource Source { get; }
}

public sealed record SetValueCommand(Guid ControlId, int Value, CommandSource Source) : IControlCommand;

public sealed record SetButtonStateCommand(Guid ControlId, bool IsOn, CommandSource Source) : IControlCommand;

public sealed record SetLedCommand(Guid ControlId, LedState Led, CommandSource Source) : IControlCommand;

public sealed record SetSubValueCommand(Guid ControlId, int SubIndex, int Value, CommandSource Source) : IControlCommand;

/// <summary>UI press on a button (momentary press / toggle activation / trigger).</summary>
public sealed record ButtonPressCommand(Guid ControlId, CommandSource Source = CommandSource.Ui) : IControlCommand;

/// <summary>UI release of a momentary button.</summary>
public sealed record ButtonReleaseCommand(Guid ControlId, CommandSource Source = CommandSource.Ui) : IControlCommand;

/// <summary>Relative knob turn; SubIndex targets a mini knob inside a bank.</summary>
public sealed record KnobDeltaCommand(Guid ControlId, int? SubIndex, int Delta, CommandSource Source = CommandSource.Ui) : IControlCommand;
