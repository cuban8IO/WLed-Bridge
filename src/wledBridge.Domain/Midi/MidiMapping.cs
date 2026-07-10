using wledBridge.Domain.Common;

namespace wledBridge.Domain.Midi;

public class MidiMapping : BaseEntity
{
    public required string Name { get; set; }
    public required MidiTrigger Trigger { get; set; }
    public required Guid WledDeviceId { get; set; }
    public bool IsEnabled { get; set; } = true;
}
