using wledBridge.Domain.Common;

namespace wledBridge.Domain.Controllers;

public class ControllerDevice : BaseEntity
{
    public required string Name { get; set; }
    public required string DriverKey { get; set; }
    public string? MidiInputDeviceName { get; set; }
    public string? MidiOutputDeviceName { get; set; }
    public bool IsEnabled { get; set; } = true;
}
