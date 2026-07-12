namespace wledBridge.VirtualMixer.Models;

/// <summary>
/// Persisted definition of a virtual mixer. Geometry uses integer logical pixels
/// (1 logical px = 1 CSS px at scale 1.0); rendering may scale but never mutates these values.
/// A mixer always contains at least one group (the lane model): groups are ordered vertical
/// lanes spanning the full mixer height, controls are positioned relative to their group.
/// </summary>
public class MixerDefinition
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int LogicalWidth { get; set; } = 1200;
    public int LogicalHeight { get; set; } = 400;
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public List<MixerGroup> Groups { get; set; } = [];
    public List<ControlBinding> Bindings { get; set; } = [];

    public IEnumerable<ControlInstance> AllControls => Groups.SelectMany(g => g.Controls);

    public MixerGroup? FindGroupOf(Guid controlId) =>
        Groups.FirstOrDefault(g => g.Controls.Any(c => c.Id == controlId));

    public ControlInstance? FindControl(Guid controlId) =>
        AllControls.FirstOrDefault(c => c.Id == controlId);
}
