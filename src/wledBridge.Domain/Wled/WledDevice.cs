using wledBridge.Domain.Common;

namespace wledBridge.Domain.Wled;

public class WledDevice : BaseEntity
{
    public required string Name { get; set; }
    public required string Host { get; set; }
    public bool IsEnabled { get; set; } = true;
}
