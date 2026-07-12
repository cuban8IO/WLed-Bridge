using wledBridge.VirtualMixer.Models;

namespace wledBridge.VirtualMixer.Controls;

/// <summary>
/// Handed to settings editor components by the config sidebar. Editors never mutate the
/// instance directly - every edit goes through <see cref="Update"/>, which routes it into the
/// designer's undoable command pipeline (mergeKey collapses rapid edits into one undo step).
/// </summary>
public sealed record ControlEditContext(ControlInstance Instance, Action<Action<ControlInstance>, string> Update);
