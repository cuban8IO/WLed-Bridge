namespace wledBridge.VirtualMixer.Models;

/// <summary>A saved, reusable control configuration (no position - placed like a new control).</summary>
public class ControlTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string TypeKey { get; set; }
    public string? Label { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string? BackgroundColorHex { get; set; }
    public required ControlSettingsBase Settings { get; set; }
}
