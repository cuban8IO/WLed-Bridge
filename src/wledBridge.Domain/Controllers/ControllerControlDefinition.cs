using wledBridge.Domain.Common;

namespace wledBridge.Domain.Controllers;

public class ControllerControlDefinition : BaseEntity
{
    public required Guid ControllerDeviceId { get; set; }
    public required string Name { get; set; }
    public required ControlType Type { get; set; }
    public required int Channel { get; set; }
    public required int CommandCode { get; set; }
    public required int DataNumber { get; set; }
    public required LedCapability LedCapability { get; set; }
    public bool IsRelative { get; set; }
}
