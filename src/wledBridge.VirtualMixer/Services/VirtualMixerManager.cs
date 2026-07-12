using wledBridge.VirtualMixer.Events;
using wledBridge.VirtualMixer.Models;
using wledBridge.VirtualMixer.Persistence;
using wledBridge.VirtualMixer.Persistence.Serialization;
using wledBridge.VirtualMixer.State;

namespace wledBridge.VirtualMixer.Services;

/// <summary>
/// Singleton mixer manager. Caches definitions in memory (loaded lazily from the store),
/// enforces the single-active-mixer rule atomically and keeps the runtime state engine in
/// sync (register on activate/save, unregister on delete).
/// </summary>
internal sealed class VirtualMixerManager(
    IVirtualMixerStore store,
    MixerJsonSerializer serializer,
    MixerRuntimeState runtime) : IVirtualMixerManager
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Dictionary<Guid, MixerDefinition>? _mixers;

    public event EventHandler<MixerActivatedEventArgs>? ActiveMixerChanged;
    public event EventHandler? MixersChanged;

    public async Task<IReadOnlyList<MixerInfo>> GetMixersAsync()
    {
        var mixers = await EnsureLoadedAsync();
        return [.. mixers.Values
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .Select(ToInfo)];
    }

    public async Task<MixerDefinition?> GetMixerAsync(Guid id)
    {
        var mixers = await EnsureLoadedAsync();
        return mixers.GetValueOrDefault(id);
    }

    public async Task<MixerDefinition?> GetActiveMixerAsync()
    {
        var mixers = await EnsureLoadedAsync();
        return mixers.Values.FirstOrDefault(m => m.IsActive);
    }

    public async Task<Guid> CreateMixerAsync(string name, string? description = null)
    {
        await EnsureLoadedAsync();
        await _gate.WaitAsync();
        try
        {
            var mixer = new MixerDefinition
            {
                Name = name,
                Description = description
            };
            mixer.Groups.Add(new MixerGroup { Label = "Standard", Order = 0, Width = mixer.LogicalWidth });

            _mixers![mixer.Id] = mixer;
            await store.SaveMixerAsync(mixer);
            return mixer.Id;
        }
        finally
        {
            _gate.Release();
            MixersChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task SaveMixerAsync(MixerDefinition mixer)
    {
        await EnsureLoadedAsync();
        await _gate.WaitAsync();
        try
        {
            if (mixer.Groups.Count == 0)
            {
                mixer.Groups.Add(new MixerGroup { Label = "Standard", Order = 0, Width = mixer.LogicalWidth });
            }

            _mixers![mixer.Id] = mixer;
            await store.SaveMixerAsync(mixer);

            // Refresh runtime states (keeps values of unchanged control IDs).
            if (runtime.IsMixerRegistered(mixer.Id))
            {
                runtime.RegisterMixer(mixer);
            }
        }
        finally
        {
            _gate.Release();
            MixersChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task DeleteMixerAsync(Guid id)
    {
        await EnsureLoadedAsync();
        await _gate.WaitAsync();
        var wasActive = false;
        try
        {
            if (_mixers!.Remove(id, out var removed))
            {
                wasActive = removed.IsActive;
                runtime.UnregisterMixer(id);
                await store.DeleteMixerAsync(id);
            }
        }
        finally
        {
            _gate.Release();
            MixersChanged?.Invoke(this, EventArgs.Empty);
            if (wasActive)
            {
                ActiveMixerChanged?.Invoke(this, new MixerActivatedEventArgs { ActiveMixer = null });
            }
        }
    }

    public async Task ActivateMixerAsync(Guid id)
    {
        await EnsureLoadedAsync();
        await _gate.WaitAsync();
        MixerDefinition? activated = null;
        try
        {
            if (!_mixers!.TryGetValue(id, out activated))
            {
                throw new KeyNotFoundException($"Mixer {id} existiert nicht.");
            }

            foreach (var mixer in _mixers.Values)
            {
                var shouldBeActive = mixer.Id == id;
                if (mixer.IsActive != shouldBeActive)
                {
                    mixer.IsActive = shouldBeActive;
                    await store.SaveMixerAsync(mixer);
                }
            }

            runtime.RegisterMixer(activated);
        }
        finally
        {
            _gate.Release();
            MixersChanged?.Invoke(this, EventArgs.Empty);
            if (activated is not null)
            {
                ActiveMixerChanged?.Invoke(this, new MixerActivatedEventArgs { ActiveMixer = ToInfo(activated) });
            }
        }
    }

    public async Task<string> ExportMixerAsync(Guid id)
    {
        var mixer = await GetMixerAsync(id)
            ?? throw new KeyNotFoundException($"Mixer {id} existiert nicht.");
        return serializer.SerializeMixer(mixer);
    }

    public async Task<Guid> ImportMixerAsync(string json)
    {
        var imported = serializer.DeserializeMixer(json);

        // New identities everywhere; imported mixers are never active.
        imported.Id = Guid.NewGuid();
        imported.IsActive = false;

        var idMap = new Dictionary<Guid, Guid>();
        foreach (var group in imported.Groups)
        {
            group.Id = Guid.NewGuid();
            foreach (var control in group.Controls)
            {
                var newId = Guid.NewGuid();
                idMap[control.Id] = newId;
                control.Id = newId;
            }
        }

        foreach (var binding in imported.Bindings)
        {
            binding.Id = Guid.NewGuid();
            binding.SourceControlId = idMap.GetValueOrDefault(binding.SourceControlId, binding.SourceControlId);
            binding.TargetControlId = idMap.GetValueOrDefault(binding.TargetControlId, binding.TargetControlId);
        }

        await SaveMixerAsync(imported);
        return imported.Id;
    }

    private async Task<Dictionary<Guid, MixerDefinition>> EnsureLoadedAsync()
    {
        if (_mixers is not null)
        {
            return _mixers;
        }

        await _gate.WaitAsync();
        try
        {
            if (_mixers is null)
            {
                var loaded = await store.LoadMixersAsync();
                _mixers = loaded.ToDictionary(m => m.Id);

                // Defensive: if multiple mixers were persisted as active, keep only the first.
                var actives = _mixers.Values.Where(m => m.IsActive).ToList();
                foreach (var extra in actives.Skip(1))
                {
                    extra.IsActive = false;
                    await store.SaveMixerAsync(extra);
                }

                if (actives.Count > 0)
                {
                    runtime.RegisterMixer(actives[0]);
                }
            }

            return _mixers;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static MixerInfo ToInfo(MixerDefinition mixer) =>
        new(mixer.Id, mixer.Name, mixer.Description, mixer.IsActive, mixer.AllControls.Count());
}
