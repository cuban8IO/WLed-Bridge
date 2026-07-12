using wledBridge.VirtualMixer.Models;

namespace wledBridge.VirtualMixer.Persistence;

/// <summary>
/// Persistence abstraction of the module. The default implementation stores JSON files;
/// hosts can replace it (e.g. with an EF-backed store) via
/// <c>VirtualMixerOptions.UseStore&lt;T&gt;()</c> without the module knowing.
/// </summary>
public interface IVirtualMixerStore
{
    Task<IReadOnlyList<MixerDefinition>> LoadMixersAsync(CancellationToken ct = default);
    Task SaveMixerAsync(MixerDefinition mixer, CancellationToken ct = default);
    Task DeleteMixerAsync(Guid mixerId, CancellationToken ct = default);

    Task<IReadOnlyList<ColorPreset>> LoadColorPresetsAsync(CancellationToken ct = default);
    Task SaveColorPresetsAsync(IReadOnlyList<ColorPreset> presets, CancellationToken ct = default);

    Task<IReadOnlyList<ControlTemplate>> LoadTemplatesAsync(CancellationToken ct = default);
    Task SaveTemplatesAsync(IReadOnlyList<ControlTemplate> templates, CancellationToken ct = default);
}
