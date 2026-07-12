using wledBridge.VirtualMixer.Controls;
using wledBridge.VirtualMixer.Models;
using wledBridge.VirtualMixer.Persistence.Serialization;
using wledBridge.VirtualMixer.Services;
using wledBridge.VirtualMixer.State;

namespace wledBridge.VirtualMixer.Designer;

internal sealed record Snapline(bool Vertical, int Position);

/// <summary>
/// Central, UI-free designer logic for one open mixer: works on a deep-cloned working copy,
/// routes every mutation through undoable commands, maintains selection / overlap / snaplines
/// and persists via the manager on explicit save.
/// </summary>
internal sealed class DesignerState
{
    private const int SnapTolerance = 4;
    private static readonly TimeSpan MergeWindow = TimeSpan.FromSeconds(1.5);

    private readonly IVirtualMixerManager _manager;
    private readonly MixerJsonSerializer _serializer;
    private readonly ControlRegistry _registry;
    private readonly DesignerClipboard _clipboard;
    private readonly MixerRuntimeState _runtime;

    private readonly List<IDesignerCommand> _undoStack = [];
    private readonly List<IDesignerCommand> _redoStack = [];
    private DateTime _lastPush = DateTime.MinValue;

    public DesignerState(
        IVirtualMixerManager manager,
        MixerJsonSerializer serializer,
        ControlRegistry registry,
        DesignerClipboard clipboard,
        MixerRuntimeState runtime,
        MixerDefinition savedMixer)
    {
        _manager = manager;
        _serializer = serializer;
        _registry = registry;
        _clipboard = clipboard;
        _runtime = runtime;
        Mixer = serializer.Clone(savedMixer);
        _runtime.RegisterMixer(Mixer);
        RecomputeOverlaps();
    }

    public MixerDefinition Mixer { get; }

    public bool EditMode { get; set; }

    public bool IsDirty { get; private set; }

    public HashSet<Guid> Selection { get; } = [];

    public Guid? ConfigControlId { get; private set; }

    public Guid? ConfigGroupId { get; private set; }

    public HashSet<Guid> Overlaps { get; private set; } = [];

    public IReadOnlyList<Snapline> ActiveSnaplines { get; private set; } = [];

    /// <summary>Pending snap adjustment (applied to the drop delta), computed during drag.</summary>
    public (int Dx, int Dy) SnapAdjustment { get; private set; }

    public string? PendingToolboxTypeKey { get; set; }

    public Guid? PendingTemplateId { get; set; }

    public bool CanUndo => _undoStack.Count > 0;

    public bool CanRedo => _redoStack.Count > 0;

    public event Action? Changed;

    public void NotifyChanged() => Changed?.Invoke();

    // ------------------------------------------------------------- commands / undo / redo

    public void Execute(IDesignerCommand command)
    {
        command.Apply(Mixer);
        _redoStack.Clear();

        if (command.MergeKey is not null &&
            _undoStack.Count > 0 &&
            _undoStack[^1].MergeKey == command.MergeKey &&
            DateTime.Now - _lastPush < MergeWindow)
        {
            _undoStack[^1].MergeWith(command);
        }
        else
        {
            _undoStack.Add(command);
        }

        _lastPush = DateTime.Now;
        AfterMutation();
    }

    public void Undo()
    {
        if (_undoStack.Count == 0)
        {
            return;
        }

        var command = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        command.Revert(Mixer);
        _redoStack.Add(command);
        AfterMutation();
    }

    public void Redo()
    {
        if (_redoStack.Count == 0)
        {
            return;
        }

        var command = _redoStack[^1];
        _redoStack.RemoveAt(_redoStack.Count - 1);
        command.Apply(Mixer);
        _undoStack.Add(command);
        AfterMutation();
    }

    private void AfterMutation()
    {
        IsDirty = true;
        Selection.RemoveWhere(id => Mixer.FindControl(id) is null);
        if (ConfigControlId is { } configId && Mixer.FindControl(configId) is null)
        {
            ConfigControlId = null;
        }

        RecomputeOverlaps();
        _runtime.RegisterMixer(Mixer);
        NotifyChanged();
    }

    public async Task SaveAsync()
    {
        await _manager.SaveMixerAsync(_serializer.Clone(Mixer));
        IsDirty = false;
        NotifyChanged();
    }

    // ------------------------------------------------------------- selection / config

    public void Select(Guid controlId, bool additive)
    {
        if (!additive)
        {
            Selection.Clear();
        }

        if (!Selection.Add(controlId) && additive)
        {
            Selection.Remove(controlId);
        }

        ApplyConfigFromSelection();
        NotifyChanged();
    }

    public void SelectRect(int x, int y, int width, int height, bool additive)
    {
        if (!additive)
        {
            Selection.Clear();
        }

        foreach (var group in Mixer.Groups.Where(g => !g.IsCollapsed))
        {
            var groupX = GroupOffsetX(group);
            foreach (var control in group.Controls)
            {
                var cx = groupX + control.X;
                var cy = GroupHeaderHeight + control.Y;
                if (cx < x + width && cx + control.Width > x && cy < y + height && cy + control.Height > y)
                {
                    Selection.Add(control.Id);
                }
            }
        }

        ApplyConfigFromSelection();
        NotifyChanged();
    }

    public void ClearSelection()
    {
        Selection.Clear();
        ApplyConfigFromSelection();
        NotifyChanged();
    }

    /// <summary>
    /// The control config panel follows the selection: it shows the settings of exactly one
    /// selected control, and closes automatically when the control is deselected or the
    /// selection becomes empty/multi (so leaving a control's focus hides its settings).
    /// </summary>
    private void ApplyConfigFromSelection()
    {
        if (Selection.Count == 1)
        {
            ConfigControlId = Selection.First();
            ConfigGroupId = null;
            ConfigMixerOpen = false;
        }
        else if (ConfigControlId is not null)
        {
            ConfigControlId = null;
        }
    }

    public void OpenConfig(Guid controlId)
    {
        if (!Selection.Contains(controlId) || Selection.Count != 1)
        {
            Selection.Clear();
            Selection.Add(controlId);
        }

        ConfigControlId = controlId;
        ConfigGroupId = null;
        ConfigMixerOpen = false;
        NotifyChanged();
    }

    public void OpenGroupConfig(Guid groupId)
    {
        Selection.Clear();
        ConfigGroupId = groupId;
        ConfigControlId = null;
        ConfigMixerOpen = false;
        NotifyChanged();
    }

    public bool ConfigMixerOpen { get; private set; }

    public void OpenMixerConfig()
    {
        Selection.Clear();
        ConfigMixerOpen = true;
        ConfigControlId = null;
        ConfigGroupId = null;
        NotifyChanged();
    }

    public void CloseConfig()
    {
        ConfigControlId = null;
        ConfigGroupId = null;
        ConfigMixerOpen = false;
        NotifyChanged();
    }

    public void MoveControlToGroup(Guid controlId, Guid targetGroupId)
    {
        var group = Mixer.FindGroupOf(controlId);
        var control = Mixer.FindControl(controlId);
        if (group is null || control is null || group.Id == targetGroupId)
        {
            return;
        }

        var target = Mixer.Groups.FirstOrDefault(g => g.Id == targetGroupId);
        if (target is null)
        {
            return;
        }

        var (x, y) = ClampToGroup(target, control, control.X, control.Y);
        Execute(new MoveControlsCommand([(controlId, group.Id, control.X, control.Y, targetGroupId, x, y)]));
    }

    // ------------------------------------------------------------- geometry helpers

    public const int GroupHeaderHeight = 22;

    public int GroupOffsetX(MixerGroup group) =>
        Mixer.Groups.Where(g => g.Order < group.Order).Sum(g => g.EffectiveWidth);

    public MixerGroup? GroupAtX(int mixerX)
    {
        var offset = 0;
        foreach (var group in Mixer.Groups.OrderBy(g => g.Order))
        {
            if (mixerX < offset + group.EffectiveWidth)
            {
                return group.IsCollapsed ? null : group;
            }

            offset += group.EffectiveWidth;
        }

        return Mixer.Groups.OrderBy(g => g.Order).LastOrDefault(g => !g.IsCollapsed);
    }

    private (int X, int Y) ClampToGroup(MixerGroup group, ControlInstance control, int x, int y)
    {
        var maxX = Math.Max(0, group.Width - control.Width);
        var maxY = Math.Max(0, Mixer.LogicalHeight - GroupHeaderHeight - control.Height);
        return (Math.Clamp(x, 0, maxX), Math.Clamp(y, 0, maxY));
    }

    /// <summary>Height (logical px) of the external caption rendered below labelled controls.</summary>
    public const int ExternalLabelHeight = 16;

    /// <summary>True if a control renders an external caption below it (so its collision box grows).</summary>
    public static bool HasExternalLabel(ControlInstance control) =>
        !string.IsNullOrEmpty(control.Label) &&
        control.TypeKey is not (BuiltInControlTypes.Button or BuiltInControlTypes.Line or BuiltInControlTypes.Label);

    private void RecomputeOverlaps()
    {
        var overlaps = new HashSet<Guid>();

        foreach (var group in Mixer.Groups)
        {
            var controls = group.Controls;
            for (var i = 0; i < controls.Count; i++)
            {
                for (var j = i + 1; j < controls.Count; j++)
                {
                    var a = controls[i];
                    var b = controls[j];
                    // The collision box includes the external label (when present), so labels
                    // that stick out below a control also trigger the overlap warning.
                    var ah = a.Height + (HasExternalLabel(a) ? ExternalLabelHeight : 0);
                    var bh = b.Height + (HasExternalLabel(b) ? ExternalLabelHeight : 0);
                    if (a.X < b.X + b.Width && a.X + a.Width > b.X &&
                        a.Y < b.Y + bh && a.Y + ah > b.Y)
                    {
                        overlaps.Add(a.Id);
                        overlaps.Add(b.Id);
                    }
                }
            }
        }

        Overlaps = overlaps;
    }

    // ------------------------------------------------------------- add / remove / move / resize

    public ControlInstance CreateControl(string typeKey)
    {
        var descriptor = _registry.Get(typeKey);
        return new ControlInstance
        {
            TypeKey = typeKey,
            Name = NextName(descriptor.DisplayName),
            Width = descriptor.SizeRules.DefaultWidth,
            Height = descriptor.SizeRules.DefaultHeight,
            Settings = descriptor.CreateDefaultSettings()
        };
    }

    private string NextName(string baseName)
    {
        var existing = Mixer.AllControls.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var index = 1;
        while (existing.Contains($"{baseName} {index}"))
        {
            index++;
        }

        return $"{baseName} {index}";
    }

    public void AddControl(ControlInstance control, MixerGroup group, int x, int y)
    {
        (control.X, control.Y) = ClampToGroup(group, control, x, y);
        Execute(new AddControlsCommand(group.Id, [control]));
        Selection.Clear();
        Selection.Add(control.Id);
        NotifyChanged();
    }

    public void RemoveSelection()
    {
        if (Selection.Count > 0)
        {
            Execute(new RemoveControlsCommand(new HashSet<Guid>(Selection)));
        }
    }

    /// <summary>Commits a drag of the current selection by (dx, dy) with snapping applied.</summary>
    public void CommitMove(int dx, int dy)
    {
        dx += SnapAdjustment.Dx;
        dy += SnapAdjustment.Dy;
        ActiveSnaplines = [];
        SnapAdjustment = (0, 0);

        if (dx == 0 && dy == 0)
        {
            NotifyChanged();
            return;
        }

        var moves = new List<(Guid, Guid, int, int, Guid, int, int)>();

        foreach (var id in Selection)
        {
            var group = Mixer.FindGroupOf(id);
            var control = Mixer.FindControl(id);
            if (group is null || control is null)
            {
                continue;
            }

            // Determine the target group from the moved control's mixer-space position.
            var mixerX = GroupOffsetX(group) + control.X + dx;
            var targetGroup = GroupAtX(mixerX + (control.Width / 2)) ?? group;

            var newLocalX = mixerX - GroupOffsetX(targetGroup);
            var newLocalY = control.Y + dy;
            var (clampedX, clampedY) = ClampToGroup(targetGroup, control, newLocalX, newLocalY);

            moves.Add((id, group.Id, control.X, control.Y, targetGroup.Id, clampedX, clampedY));
        }

        if (moves.Count > 0)
        {
            Execute(new MoveControlsCommand(moves));
        }
    }

    /// <summary>Nudges the selection by keyboard (already in logical pixels).</summary>
    public void Nudge(int dx, int dy) => CommitMove(dx, dy);

    /// <summary>Transient rect shown while a resize drag is in progress (no command yet).</summary>
    public (Guid ControlId, int X, int Y, int W, int H)? ResizePreview { get; private set; }

    public (int X, int Y, int W, int H)? ComputeResize(Guid controlId, string handle, int dx, int dy)
    {
        var control = Mixer.FindControl(controlId);
        var group = Mixer.FindGroupOf(controlId);
        if (control is null || group is null)
        {
            return null;
        }

        var rules = _registry.Get(control.TypeKey).SizeRules;
        var before = (X: control.X, Y: control.Y, Width: control.Width, Height: control.Height);

        var x = control.X;
        var y = control.Y;
        var w = control.Width;
        var h = control.Height;

        if (handle.Contains('e')) w += dx;
        if (handle.Contains('s')) h += dy;
        if (handle.Contains('w')) { x += dx; w -= dx; }
        if (handle.Contains('n')) { y += dy; h -= dy; }

        (w, h) = rules.Clamp(w, h);

        // Re-anchor for west/north handles after clamping.
        if (handle.Contains('w')) x = before.X + (before.Width - w);
        if (handle.Contains('n')) y = before.Y + (before.Height - h);

        x = Math.Clamp(x, 0, Math.Max(0, group.Width - w));
        y = Math.Clamp(y, 0, Math.Max(0, Mixer.LogicalHeight - GroupHeaderHeight - h));

        return (x, y, w, h);
    }

    public void PreviewResize(Guid controlId, string handle, int dx, int dy)
    {
        if (ComputeResize(controlId, handle, dx, dy) is { } rect)
        {
            ResizePreview = (controlId, rect.X, rect.Y, rect.W, rect.H);
            NotifyChanged();
        }
    }

    public void CommitResize(Guid controlId, string handle, int dx, int dy)
    {
        ResizePreview = null;

        var control = Mixer.FindControl(controlId);
        if (control is null || ComputeResize(controlId, handle, dx, dy) is not { } after)
        {
            NotifyChanged();
            return;
        }

        var before = (control.X, control.Y, control.Width, control.Height);
        if (after != before)
        {
            Execute(new ResizeControlCommand(controlId, before, after));
        }
        else
        {
            NotifyChanged();
        }
    }

    // ------------------------------------------------------------- snaplines (during drag)

    /// <summary>Computes snaplines + magnetic adjustment for the selection dragged by (dx, dy).</summary>
    public void UpdateDragSnap(int dx, int dy)
    {
        if (Selection.Count == 0)
        {
            return;
        }

        // Bounding box of the dragged selection in mixer space.
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        MixerGroup? primaryGroup = null;

        foreach (var id in Selection)
        {
            var group = Mixer.FindGroupOf(id);
            var control = Mixer.FindControl(id);
            if (group is null || control is null)
            {
                continue;
            }

            primaryGroup ??= group;
            var x = GroupOffsetX(group) + control.X + dx;
            var y = GroupHeaderHeight + control.Y + dy;
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x + control.Width);
            maxY = Math.Max(maxY, y + control.Height);
        }

        if (primaryGroup is null)
        {
            return;
        }

        var candidatesX = new List<int> { minX, (minX + maxX) / 2, maxX };
        var candidatesY = new List<int> { minY, (minY + maxY) / 2, maxY };

        var lines = new List<Snapline>();
        var snapDx = 0;
        var snapDy = 0;
        var bestDx = SnapTolerance + 1;
        var bestDy = SnapTolerance + 1;

        foreach (var group in Mixer.Groups.Where(g => !g.IsCollapsed))
        {
            var groupX = GroupOffsetX(group);
            foreach (var other in group.Controls)
            {
                if (Selection.Contains(other.Id))
                {
                    continue;
                }

                var ox = groupX + other.X;
                var oy = GroupHeaderHeight + other.Y;
                int[] otherX = [ox, ox + (other.Width / 2), ox + other.Width];
                int[] otherY = [oy, oy + (other.Height / 2), oy + other.Height];

                foreach (var candidate in candidatesX)
                {
                    foreach (var target in otherX)
                    {
                        var distance = Math.Abs(candidate - target);
                        if (distance <= SnapTolerance && distance < bestDx)
                        {
                            bestDx = distance;
                            snapDx = target - candidate;
                            lines.RemoveAll(l => l.Vertical);
                            lines.Add(new Snapline(true, target));
                        }
                    }
                }

                foreach (var candidate in candidatesY)
                {
                    foreach (var target in otherY)
                    {
                        var distance = Math.Abs(candidate - target);
                        if (distance <= SnapTolerance && distance < bestDy)
                        {
                            bestDy = distance;
                            snapDy = target - candidate;
                            lines.RemoveAll(l => !l.Vertical);
                            lines.Add(new Snapline(false, target));
                        }
                    }
                }
            }
        }

        ActiveSnaplines = lines;
        SnapAdjustment = (snapDx, snapDy);
        NotifyChanged();
    }

    public void ClearDragSnap()
    {
        ActiveSnaplines = [];
        SnapAdjustment = (0, 0);
        NotifyChanged();
    }

    // ------------------------------------------------------------- clipboard / align

    public void CopySelection()
    {
        var controls = Selection
            .Select(Mixer.FindControl)
            .Where(c => c is not null)
            .Cast<ControlInstance>()
            .ToList();

        if (controls.Count > 0)
        {
            _clipboard.Copy(controls);
            NotifyChanged();
        }
    }

    public void Paste()
    {
        var controls = _clipboard.Paste();
        if (controls.Count == 0)
        {
            return;
        }

        var group = (ConfigControlId is { } id ? Mixer.FindGroupOf(id) : null)
            ?? Mixer.Groups.OrderBy(g => g.Order).First(g => !g.IsCollapsed);

        foreach (var control in controls)
        {
            control.Name = NextName(control.Name.TrimEnd("0123456789 ".ToCharArray()));
            var (x, y) = ClampToGroup(group, control, control.X + 16, control.Y + 16);
            control.X = x;
            control.Y = y;
        }

        Execute(new AddControlsCommand(group.Id, controls));
        Selection.Clear();
        foreach (var control in controls)
        {
            Selection.Add(control.Id);
        }

        NotifyChanged();
    }

    public void DuplicateSelection()
    {
        CopySelection();
        Paste();
    }

    public bool ClipboardHasContent => _clipboard.HasContent;

    public enum AlignKind { Left, Right, Top, Bottom, CenterHorizontal, CenterVertical, DistributeHorizontal, DistributeVertical }

    public void Align(AlignKind kind)
    {
        var items = Selection
            .Select(id => (Control: Mixer.FindControl(id), Group: Mixer.FindGroupOf(id)))
            .Where(t => t.Control is not null && t.Group is not null)
            .Select(t => (Control: t.Control!, Group: t.Group!))
            .ToList();

        if (items.Count < 2)
        {
            return;
        }

        var moves = new List<(Guid, Guid, int, int, Guid, int, int)>();

        void Move(ControlInstance control, MixerGroup group, int newX, int newY)
        {
            var (x, y) = ClampToGroup(group, control, newX, newY);
            if (x != control.X || y != control.Y)
            {
                moves.Add((control.Id, group.Id, control.X, control.Y, group.Id, x, y));
            }
        }

        switch (kind)
        {
            case AlignKind.Left:
                var left = items.Min(i => GroupOffsetX(i.Group) + i.Control.X);
                foreach (var (control, group) in items)
                {
                    Move(control, group, left - GroupOffsetX(group), control.Y);
                }
                break;

            case AlignKind.Right:
                var right = items.Max(i => GroupOffsetX(i.Group) + i.Control.X + i.Control.Width);
                foreach (var (control, group) in items)
                {
                    Move(control, group, right - GroupOffsetX(group) - control.Width, control.Y);
                }
                break;

            case AlignKind.Top:
                var top = items.Min(i => i.Control.Y);
                foreach (var (control, group) in items)
                {
                    Move(control, group, control.X, top);
                }
                break;

            case AlignKind.Bottom:
                var bottom = items.Max(i => i.Control.Y + i.Control.Height);
                foreach (var (control, group) in items)
                {
                    Move(control, group, control.X, bottom - control.Height);
                }
                break;

            case AlignKind.CenterHorizontal:
                var centerY = (int)items.Average(i => i.Control.Y + (i.Control.Height / 2.0));
                foreach (var (control, group) in items)
                {
                    Move(control, group, control.X, centerY - (control.Height / 2));
                }
                break;

            case AlignKind.CenterVertical:
                var centerX = (int)items.Average(i => GroupOffsetX(i.Group) + i.Control.X + (i.Control.Width / 2.0));
                foreach (var (control, group) in items)
                {
                    Move(control, group, centerX - GroupOffsetX(group) - (control.Width / 2), control.Y);
                }
                break;

            case AlignKind.DistributeHorizontal:
                var byX = items.OrderBy(i => GroupOffsetX(i.Group) + i.Control.X).ToList();
                var totalWidth = byX.Sum(i => i.Control.Width);
                var spanX = (GroupOffsetX(byX[^1].Group) + byX[^1].Control.X + byX[^1].Control.Width)
                            - (GroupOffsetX(byX[0].Group) + byX[0].Control.X);
                var gapX = byX.Count > 1 ? Math.Max(0, (spanX - totalWidth) / (byX.Count - 1)) : 0;
                var cursorX = GroupOffsetX(byX[0].Group) + byX[0].Control.X;
                foreach (var (control, group) in byX)
                {
                    Move(control, group, cursorX - GroupOffsetX(group), control.Y);
                    cursorX += control.Width + gapX;
                }
                break;

            case AlignKind.DistributeVertical:
                var byY = items.OrderBy(i => i.Control.Y).ToList();
                var totalHeight = byY.Sum(i => i.Control.Height);
                var spanY = (byY[^1].Control.Y + byY[^1].Control.Height) - byY[0].Control.Y;
                var gapY = byY.Count > 1 ? Math.Max(0, (spanY - totalHeight) / (byY.Count - 1)) : 0;
                var cursorY = byY[0].Control.Y;
                foreach (var (control, group) in byY)
                {
                    Move(control, group, control.X, cursorY);
                    cursorY += control.Height + gapY;
                }
                break;
        }

        if (moves.Count > 0)
        {
            Execute(new MoveControlsCommand(moves));
        }
    }

    // ------------------------------------------------------------- sidebar edits

    /// <summary>Applies a property edit to a control as an undoable (mergeable) command.</summary>
    public void UpdateControl(Guid controlId, Action<ControlInstance> mutate, string mergeKey)
    {
        var control = Mixer.FindControl(controlId);
        if (control is null)
        {
            return;
        }

        var before = _serializer.Clone(control);
        mutate(control);
        Execute(new ControlSnapshotCommand(_serializer, before, _serializer.Clone(control), $"{controlId}:{mergeKey}"));
    }

    public void UpdateGroup(Guid groupId, Action<MixerGroup> mutate, string mergeKey)
    {
        var group = Mixer.Groups.FirstOrDefault(g => g.Id == groupId);
        if (group is null)
        {
            return;
        }

        var before = GroupProps.From(group);
        mutate(group);
        Execute(new GroupPropsCommand(groupId, before, GroupProps.From(group), $"{groupId}:{mergeKey}"));
    }

    public void UpdateMixerProps(Action<MixerDefinition> mutate, string mergeKey)
    {
        var before = MixerProps.From(Mixer);
        mutate(Mixer);
        Execute(new MixerPropsCommand(before, MixerProps.From(Mixer), mergeKey));
    }

    public void UpdateBindings(Action<List<ControlBinding>> mutate)
    {
        var before = Mixer.Bindings.Select(b => _serializer.Clone(b)).ToList();
        var working = Mixer.Bindings.Select(b => _serializer.Clone(b)).ToList();
        mutate(working);
        Execute(new BindingsSnapshotCommand(before, working));
    }

    // ------------------------------------------------------------- groups

    public void AddGroup()
    {
        var group = new MixerGroup
        {
            Label = $"Gruppe {Mixer.Groups.Count + 1}",
            Order = Mixer.Groups.Count == 0 ? 0 : Mixer.Groups.Max(g => g.Order) + 1,
            Width = 300
        };
        Execute(new AddGroupCommand(group));
        OpenGroupConfig(group.Id);
    }

    public void RemoveGroup(Guid groupId)
    {
        if (Mixer.Groups.Count <= 1)
        {
            return; // a mixer always keeps at least one group
        }

        Execute(new RemoveGroupCommand(groupId));
    }

    public void MoveGroup(Guid groupId, int direction)
    {
        var ordered = Mixer.Groups.OrderBy(g => g.Order).ToList();
        var index = ordered.FindIndex(g => g.Id == groupId);
        var targetIndex = index + direction;
        if (index < 0 || targetIndex < 0 || targetIndex >= ordered.Count)
        {
            return;
        }

        var group = ordered[index];
        var neighbor = ordered[targetIndex];
        var before = GroupProps.From(group);
        var neighborBefore = GroupProps.From(neighbor);

        (group.Order, neighbor.Order) = (neighbor.Order, group.Order);

        // Two prop commands would be two undo steps; use snapshots via one composite instead.
        Execute(new SwapGroupOrderCommand(group.Id, neighbor.Id, before.Order, neighborBefore.Order));
    }

    private sealed class SwapGroupOrderCommand(Guid a, Guid b, int aOrder, int bOrder) : IDesignerCommand
    {
        public string Description => "Gruppen umsortieren";

        public void Apply(MixerDefinition mixer)
        {
            mixer.Groups.First(g => g.Id == a).Order = bOrder;
            mixer.Groups.First(g => g.Id == b).Order = aOrder;
        }

        public void Revert(MixerDefinition mixer)
        {
            mixer.Groups.First(g => g.Id == a).Order = aOrder;
            mixer.Groups.First(g => g.Id == b).Order = bOrder;
        }
    }
}
