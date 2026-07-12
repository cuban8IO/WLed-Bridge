using wledBridge.VirtualMixer.Models;

namespace wledBridge.VirtualMixer.Services;

/// <summary>
/// Module-owned color management: persisted presets plus a transient "recently used" row
/// (max 8, per app lifetime, never persisted).
/// </summary>
public interface IColorPresetService
{
    Task<IReadOnlyList<ColorPreset>> GetPresetsAsync();
    Task SavePresetAsync(ColorPreset preset);
    Task DeletePresetAsync(Guid id);

    IReadOnlyList<string> RecentColors { get; }
    void PushRecentColor(string colorHex);

    event EventHandler? Changed;
}
