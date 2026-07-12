namespace wledBridge.VirtualMixer.Models;

/// <summary>
/// A vertical lane spanning the full mixer height. Groups are laid out left-to-right by
/// <see cref="Order"/>; a group's X offset is the sum of the effective widths of all groups
/// before it, so collapsing a group automatically shifts the following ones.
/// </summary>
public class MixerGroup
{
    /// <summary>Rendered width of a collapsed group in logical pixels.</summary>
    public const int CollapsedWidth = 32;

    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Label { get; set; }
    public int Order { get; set; }
    public int Width { get; set; } = 400;
    public string? BackgroundColorHex { get; set; }
    public bool IsCollapsed { get; set; }
    public List<ControlInstance> Controls { get; set; } = [];

    public IEnumerable<ControlInstance> CollapsedVisibleControls =>
        Controls.Where(c => c.KeepVisibleWhenCollapsed);

    /// <summary>
    /// Rendered width: the full width when expanded; when collapsed, the narrow lane width -
    /// unless some controls keep rendering, in which case it grows just enough to fit them.
    /// </summary>
    public int EffectiveWidth
    {
        get
        {
            if (!IsCollapsed)
            {
                return Width;
            }

            var kept = CollapsedVisibleControls.ToList();
            if (kept.Count == 0)
            {
                return CollapsedWidth;
            }

            return Math.Max(CollapsedWidth, kept.Max(c => c.X + c.Width) + 8);
        }
    }
}
