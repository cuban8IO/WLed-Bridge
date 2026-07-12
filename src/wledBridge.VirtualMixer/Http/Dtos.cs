using wledBridge.VirtualMixer.Events;

namespace wledBridge.VirtualMixer.Http;

/// <summary>
/// Serialization-stable network contract - deliberately decoupled from internal state types.
/// </summary>
public sealed record ControlEventDto(
    DateTime Timestamp,
    Guid MixerId,
    string MixerName,
    Guid ControlId,
    string ControlName,
    string ControlType,
    string EventType,
    int? SubIndex,
    string? OldValue,
    string? NewValue,
    string Source)
{
    public static ControlEventDto From(ControlEventArgs args) => new(
        args.Timestamp,
        args.MixerId,
        args.MixerName,
        args.ControlId,
        args.ControlName,
        args.TypeKey,
        args.EventName,
        args.SubIndex,
        args.OldValueText,
        args.NewValueText,
        args.Source.ToString());
}

public sealed record MixerInfoDto(Guid Id, string Name, string? Description, bool IsActive, int ControlCount);

public sealed record ControlStateDto(
    Guid ControlId,
    string ControlType,
    int? Value,
    bool? IsOn,
    int? LedBrightness,
    string? LedColorHex,
    IReadOnlyList<int>? SubValues);

public sealed record SetValueRequest(int Value);

public sealed record SetButtonStateRequest(bool IsOn);

public sealed record SetLedRequest(int Brightness, string? ColorHex);
