using wledBridge.Domain.Common;

namespace wledBridge.Domain.Controllers;

public class ControllerDevice : BaseEntity
{
    public required string Name { get; set; }
    public required string DriverKey { get; set; }
    public string? MidiInputDeviceName { get; set; }
    public string? MidiOutputDeviceName { get; set; }
    public bool IsEnabled { get; set; } = true;

    /// <remarks>Only populated when <see cref="DriverKey"/> is the generic/learned driver key.</remarks>
    public List<ControllerControlDefinition> ControlDefinitions { get; set; } = [];
}
