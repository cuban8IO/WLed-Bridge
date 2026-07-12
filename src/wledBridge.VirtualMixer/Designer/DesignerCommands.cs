using wledBridge.VirtualMixer.Models;
using wledBridge.VirtualMixer.Persistence.Serialization;

namespace wledBridge.VirtualMixer.Designer;

/// <summary>
/// One undoable designer mutation. Every change to the working copy - mouse, keyboard,
/// sidebar, group management - goes through a command, which is what makes full undo/redo
/// possible without special cases.
/// </summary>
internal interface IDesignerCommand
{
    string Description { get; }

    void Apply(MixerDefinition mixer);

    void Revert(MixerDefinition mixer);

    /// <summary>Non-null commands with equal keys merge on the undo stack (debounced edits).</summary>
    string? MergeKey => null;

    /// <summary>Merges a follow-up command with the same MergeKey into this one.</summary>
    void MergeWith(IDesignerCommand next) { }
}

internal sealed class AddControlsCommand(Guid groupId, IReadOnlyList<ControlInstance> controls) : IDesignerCommand
{
    public string Description => controls.Count == 1 ? "Control hinzufügen" : $"{controls.Count} Controls hinzufügen";

    public IReadOnlyList<ControlInstance> Controls => controls;

    public void Apply(MixerDefinition mixer)
    {
        var group = mixer.Groups.First(g => g.Id == groupId);
        group.Controls.AddRange(controls);
    }

    public void Revert(MixerDefinition mixer)
    {
        var group = mixer.Groups.First(g => g.Id == groupId);
        group.Controls.RemoveAll(c => controls.Any(a => a.Id == c.Id));
    }
}

internal sealed class RemoveControlsCommand : IDesignerCommand
{
    private readonly List<(Guid GroupId, ControlInstance Control, int Index)> _removed = [];
    private readonly List<ControlBinding> _removedBindings = [];
    private readonly IReadOnlySet<Guid> _controlIds;

    public RemoveControlsCommand(IReadOnlySet<Guid> controlIds) => _controlIds = controlIds;

    public string Description => _controlIds.Count == 1 ? "Control entfernen" : $"{_controlIds.Count} Controls entfernen";

    public void Apply(MixerDefinition mixer)
    {
        _removed.Clear();
        _removedBindings.Clear();

        foreach (var group in mixer.Groups)
        {
            for (var i = group.Controls.Count - 1; i >= 0; i--)
            {
                if (_controlIds.Contains(group.Controls[i].Id))
                {
                    _removed.Add((group.Id, group.Controls[i], i));
                    group.Controls.RemoveAt(i);
                }
            }
        }

        _removedBindings.AddRange(mixer.Bindings.Where(b =>
            _controlIds.Contains(b.SourceControlId) || _controlIds.Contains(b.TargetControlId)));
        mixer.Bindings.RemoveAll(b =>
            _controlIds.Contains(b.SourceControlId) || _controlIds.Contains(b.TargetControlId));
    }

    public void Revert(MixerDefinition mixer)
    {
        foreach (var (groupId, control, index) in _removed.OrderBy(r => r.Index))
        {
            var group = mixer.Groups.First(g => g.Id == groupId);
            group.Controls.Insert(Math.Min(index, group.Controls.Count), control);
        }

        mixer.Bindings.AddRange(_removedBindings);
    }
}

internal sealed class MoveControlsCommand(
    IReadOnlyList<(Guid ControlId, Guid OldGroupId, int OldX, int OldY, Guid NewGroupId, int NewX, int NewY)> moves)
    : IDesignerCommand
{
    public string Description => moves.Count == 1 ? "Control verschieben" : $"{moves.Count} Controls verschieben";

    public void Apply(MixerDefinition mixer)
    {
        foreach (var move in moves)
        {
            var control = Detach(mixer, move.ControlId);
            control.X = move.NewX;
            control.Y = move.NewY;
            mixer.Groups.First(g => g.Id == move.NewGroupId).Controls.Add(control);
        }
    }

    public void Revert(MixerDefinition mixer)
    {
        foreach (var move in moves)
        {
            var control = Detach(mixer, move.ControlId);
            control.X = move.OldX;
            control.Y = move.OldY;
            mixer.Groups.First(g => g.Id == move.OldGroupId).Controls.Add(control);
        }
    }

    private static ControlInstance Detach(MixerDefinition mixer, Guid controlId)
    {
        foreach (var group in mixer.Groups)
        {
            if (group.Controls.FirstOrDefault(c => c.Id == controlId) is { } control)
            {
                group.Controls.Remove(control);
                return control;
            }
        }

        throw new InvalidOperationException($"Control {controlId} nicht gefunden.");
    }
}

internal sealed class ResizeControlCommand(
    Guid controlId,
    (int X, int Y, int W, int H) before,
    (int X, int Y, int W, int H) after) : IDesignerCommand
{
    public string Description => "Control-Größe ändern";

    public void Apply(MixerDefinition mixer) => Set(mixer, after);

    public void Revert(MixerDefinition mixer) => Set(mixer, before);

    private void Set(MixerDefinition mixer, (int X, int Y, int W, int H) rect)
    {
        var control = mixer.FindControl(controlId)
            ?? throw new InvalidOperationException($"Control {controlId} nicht gefunden.");
        control.X = rect.X;
        control.Y = rect.Y;
        control.Width = rect.W;
        control.Height = rect.H;
    }
}

/// <summary>
/// Generic property edit: replaces a control's full content by serialized snapshots.
/// Used by the config sidebar; rapid consecutive edits with the same merge key collapse
/// into a single undo step.
/// </summary>
internal sealed class ControlSnapshotCommand : IDesignerCommand
{
    private readonly MixerJsonSerializer _serializer;
    private readonly Guid _controlId;
    private readonly string _beforeJson;
    private string _afterJson;

    public ControlSnapshotCommand(MixerJsonSerializer serializer, ControlInstance before, ControlInstance after, string? mergeKey)
    {
        _serializer = serializer;
        _controlId = before.Id;
        _beforeJson = serializer.Serialize(before);
        _afterJson = serializer.Serialize(after);
        MergeKey = mergeKey;
    }

    public string Description => "Control bearbeiten";

    public string? MergeKey { get; }

    public void MergeWith(IDesignerCommand next)
    {
        if (next is ControlSnapshotCommand snapshot)
        {
            _afterJson = snapshot._afterJson;
        }
    }

    public void Apply(MixerDefinition mixer) => Replace(mixer, _afterJson);

    public void Revert(MixerDefinition mixer) => Replace(mixer, _beforeJson);

    private void Replace(MixerDefinition mixer, string json)
    {
        var replacement = _serializer.Deserialize<ControlInstance>(json)!;
        foreach (var group in mixer.Groups)
        {
            var index = group.Controls.FindIndex(c => c.Id == _controlId);
            if (index >= 0)
            {
                group.Controls[index] = replacement;
                return;
            }
        }
    }
}

internal sealed record GroupProps(string Label, int Width, string? BackgroundColorHex, bool IsCollapsed, int Order)
{
    public static GroupProps From(MixerGroup group) =>
        new(group.Label, group.Width, group.BackgroundColorHex, group.IsCollapsed, group.Order);

    public void ApplyTo(MixerGroup group)
    {
        group.Label = Label;
        group.Width = Width;
        group.BackgroundColorHex = BackgroundColorHex;
        group.IsCollapsed = IsCollapsed;
        group.Order = Order;
    }
}

internal sealed class GroupPropsCommand : IDesignerCommand
{
    private readonly Guid _groupId;
    private readonly GroupProps _before;
    private GroupProps _after;

    public GroupPropsCommand(Guid groupId, GroupProps before, GroupProps after, string? mergeKey)
    {
        _groupId = groupId;
        _before = before;
        _after = after;
        MergeKey = mergeKey;
    }

    public string Description => "Gruppe bearbeiten";

    public string? MergeKey { get; }

    public void MergeWith(IDesignerCommand next)
    {
        if (next is GroupPropsCommand props)
        {
            _after = props._after;
        }
    }

    public void Apply(MixerDefinition mixer) => _after.ApplyTo(mixer.Groups.First(g => g.Id == _groupId));

    public void Revert(MixerDefinition mixer) => _before.ApplyTo(mixer.Groups.First(g => g.Id == _groupId));
}

internal sealed class AddGroupCommand(MixerGroup group) : IDesignerCommand
{
    public string Description => "Gruppe hinzufügen";

    public void Apply(MixerDefinition mixer) => mixer.Groups.Add(group);

    public void Revert(MixerDefinition mixer) => mixer.Groups.RemoveAll(g => g.Id == group.Id);
}

/// <summary>Removes a group; its controls move to the first remaining group (revertable).</summary>
internal sealed class RemoveGroupCommand(Guid groupId) : IDesignerCommand
{
    private MixerGroup? _removed;
    private Guid _targetGroupId;
    private List<Guid> _movedControlIds = [];

    public string Description => "Gruppe entfernen";

    public void Apply(MixerDefinition mixer)
    {
        _removed = mixer.Groups.First(g => g.Id == groupId);
        mixer.Groups.Remove(_removed);

        var target = mixer.Groups.OrderBy(g => g.Order).First();
        _targetGroupId = target.Id;
        _movedControlIds = [.. _removed.Controls.Select(c => c.Id)];
        target.Controls.AddRange(_removed.Controls);
        _removed.Controls = [.. _removed.Controls]; // keep our own copy for revert
    }

    public void Revert(MixerDefinition mixer)
    {
        if (_removed is null)
        {
            return;
        }

        var target = mixer.Groups.First(g => g.Id == _targetGroupId);
        target.Controls.RemoveAll(c => _movedControlIds.Contains(c.Id));
        mixer.Groups.Add(_removed);
    }
}

internal sealed class BindingsSnapshotCommand(List<ControlBinding> before, List<ControlBinding> after) : IDesignerCommand
{
    public string Description => "Bindings ändern";

    public void Apply(MixerDefinition mixer)
    {
        mixer.Bindings.Clear();
        mixer.Bindings.AddRange(after);
    }

    public void Revert(MixerDefinition mixer)
    {
        mixer.Bindings.Clear();
        mixer.Bindings.AddRange(before);
    }
}

internal sealed record MixerProps(string Name, string? Description, int LogicalWidth, int LogicalHeight)
{
    public static MixerProps From(MixerDefinition mixer) =>
        new(mixer.Name, mixer.Description, mixer.LogicalWidth, mixer.LogicalHeight);

    public void ApplyTo(MixerDefinition mixer)
    {
        mixer.Name = Name;
        mixer.Description = Description;
        mixer.LogicalWidth = LogicalWidth;
        mixer.LogicalHeight = LogicalHeight;
    }
}

internal sealed class MixerPropsCommand : IDesignerCommand
{
    private readonly MixerProps _before;
    private MixerProps _after;

    public MixerPropsCommand(MixerProps before, MixerProps after, string? mergeKey)
    {
        _before = before;
        _after = after;
        MergeKey = mergeKey;
    }

    public string Description => "Mixer-Eigenschaften ändern";

    public string? MergeKey { get; }

    public void MergeWith(IDesignerCommand next)
    {
        if (next is MixerPropsCommand props)
        {
            _after = props._after;
        }
    }

    public void Apply(MixerDefinition mixer) => _after.ApplyTo(mixer);

    public void Revert(MixerDefinition mixer) => _before.ApplyTo(mixer);
}
