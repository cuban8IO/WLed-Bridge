namespace wledBridge.VirtualMixer.Models;

/// <summary>
/// One placed control on a mixer. Common properties only - everything type-specific lives in
/// <see cref="Settings"/> (polymorphic, resolved via the control's <see cref="TypeKey"/>).
/// X/Y are relative to the owning group's content area, in integer logical pixels.
/// </summary>
public class ControlInstance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string TypeKey { get; set; }
    public required string Name { get; set; }
    public string? Label { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string? BackgroundColorHex { get; set; }
    public bool IsVisible { get; set; } = true;
    public bool IsEnabled { get; set; } = true;
    public required ControlSettingsBase Settings { get; set; }
}
