using wledBridge.Domain.Common;

namespace wledBridge.Domain.Controllers;

public class ControlMapping : BaseEntity
{
    public required Guid ControllerDeviceId { get; set; }
    public required string PhysicalControlId { get; set; }
    public required Guid VirtualControlId { get; set; }
    public bool IsEnabled { get; set; } = true;
}
