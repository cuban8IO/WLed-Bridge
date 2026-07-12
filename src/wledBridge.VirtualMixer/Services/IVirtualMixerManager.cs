using wledBridge.VirtualMixer.Events;
using wledBridge.VirtualMixer.Models;

namespace wledBridge.VirtualMixer.Services;

/// <summary>
/// Mixer management: CRUD, activation (the module itself enforces that at most one mixer is
/// active at any time) and JSON export/import.
/// </summary>
public interface IVirtualMixerManager
{
    Task<IReadOnlyList<MixerInfo>> GetMixersAsync();
    Task<MixerDefinition?> GetMixerAsync(Guid id);
    Task<MixerDefinition?> GetActiveMixerAsync();
    Task<Guid> CreateMixerAsync(string name, string? description = null);
    Task SaveMixerAsync(MixerDefinition mixer);
    Task DeleteMixerAsync(Guid id);
    Task ActivateMixerAsync(Guid id);

    /// <summary>Exports a mixer definition as indented JSON.</summary>
    Task<string> ExportMixerAsync(Guid id);

    /// <summary>Imports a definition (schema-migrated if needed); new IDs, never active.</summary>
    Task<Guid> ImportMixerAsync(string json);

    event EventHandler<MixerActivatedEventArgs>? ActiveMixerChanged;

    /// <summary>Raised after any mixer was created, saved, deleted or (de)activated.</summary>
    event EventHandler? MixersChanged;
}
