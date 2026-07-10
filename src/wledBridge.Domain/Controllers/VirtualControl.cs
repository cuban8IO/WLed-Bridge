using wledBridge.Domain.Common;

namespace wledBridge.Domain.Controllers;

public class VirtualControl : BaseEntity
{
    public required Guid VirtualMixerId { get; set; }
    public required string Name { get; set; }
    public required ControlType Type { get; set; }
    public double Value { get; set; }
    public bool IsOn { get; set; }
    public string? Color { get; set; }
}
