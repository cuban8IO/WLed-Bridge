using wledBridge.Domain.Common;

namespace wledBridge.Domain.Controllers;

public class VirtualMixer : BaseEntity
{
    public required string Name { get; set; }

    public List<VirtualControl> Controls { get; set; } = [];
}
