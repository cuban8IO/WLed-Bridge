using wledBridge.Domain.Controllers;

namespace wledBridge.Web.Devices;

public class ControllerConfigurationExport
{
    public required string Name { get; set; }
    public required string DriverKey { get; set; }
    public bool IsTemplate { get; set; }
    public string? MidiInputDeviceName { get; set; }
    public string? MidiOutputDeviceName { get; set; }
    public List<ControlDefinitionExport> Controls { get; set; } = [];
}

public class ControlDefinitionExport
{
    public required string Name { get; set; }
    public required ControlType Type { get; set; }
    public required LedCapability LedCapability { get; set; }
    public bool IsRelative { get; set; }
    public int Channel { get; set; }
    public int CommandCode { get; set; }
    public int DataNumber { get; set; }
}
