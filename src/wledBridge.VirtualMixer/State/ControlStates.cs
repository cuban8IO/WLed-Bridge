namespace wledBridge.VirtualMixer.State;

/// <summary>
/// Base for in-memory runtime state of one placed control. Runtime state is transient by design
/// (never persisted) - the mixer definition and the live values are strictly separated.
/// Instances are only ever mutated on the runtime engine's single consumer thread.
/// </summary>
public abstract class ControlStateBase
{
    public required Guid ControlId { get; init; }
}

/// <summary>Button: pressed/toggle state plus LED runtime values.</summary>
public sealed class ButtonState : ControlStateBase
{
    public bool IsOn { get; set; }
    public int LedBrightness { get; set; }
    public string? LedColorHex { get; set; }
}

/// <summary>Single-value controls: fader, absolute knob, statusbar.</summary>
public sealed class ValueState : ControlStateBase
{
    public int Value { get; set; }

    /// <summary>Runtime color override (statusbar RGB), set via LED commands.</summary>
    public string? ColorHex { get; set; }
}

/// <summary>Knob bank: one value per mini knob.</summary>
public sealed class SubValueState : ControlStateBase
{
    public required int[] Values { get; init; }
}

/// <summary>Immutable public read view of a control's runtime state.</summary>
public sealed record ControlStateSnapshot(
    Guid ControlId,
    string TypeKey,
    int? Value,
    bool? IsOn,
    int? LedBrightness,
    string? LedColorHex,
    IReadOnlyList<int>? SubValues);
