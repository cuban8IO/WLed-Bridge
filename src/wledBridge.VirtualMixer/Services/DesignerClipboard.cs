using wledBridge.VirtualMixer.Models;
using wledBridge.VirtualMixer.Persistence.Serialization;

namespace wledBridge.VirtualMixer.Services;

/// <summary>
/// App-wide clipboard for designer copy/paste. Content is serialized JSON, so pasting works
/// across mixers (and across browser circuits of the same app instance).
/// </summary>
internal sealed class DesignerClipboard(MixerJsonSerializer serializer)
{
    private string? _json;

    public bool HasContent => _json is not null;

    public void Copy(IEnumerable<ControlInstance> controls) =>
        _json = serializer.Serialize(controls.ToList());

    /// <summary>Returns deep clones with fresh IDs, ready to insert.</summary>
    public List<ControlInstance> Paste()
    {
        if (_json is null)
        {
            return [];
        }

        var controls = serializer.Deserialize<List<ControlInstance>>(_json) ?? [];
        foreach (var control in controls)
        {
            control.Id = Guid.NewGuid();
        }

        return controls;
    }
}
