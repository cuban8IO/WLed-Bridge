using Microsoft.Extensions.Options;
using wledBridge.VirtualMixer.Models;
using wledBridge.VirtualMixer.Persistence.Serialization;

namespace wledBridge.VirtualMixer.Persistence;

/// <summary>
/// Default store: one JSON file per mixer plus colorpresets.json / templates.json in a
/// configurable directory (VirtualMixerOptions.StoragePath).
/// </summary>
internal sealed class JsonFileVirtualMixerStore(IOptions<VirtualMixerOptions> options, MixerJsonSerializer serializer)
    : IVirtualMixerStore
{
    private readonly string _root = options.Value.StoragePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private string MixersDirectory => Path.Combine(_root, "mixers");
    private string ColorPresetsFile => Path.Combine(_root, "colorpresets.json");
    private string TemplatesFile => Path.Combine(_root, "templates.json");

    public async Task<IReadOnlyList<MixerDefinition>> LoadMixersAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (!Directory.Exists(MixersDirectory))
            {
                return [];
            }

            var mixers = new List<MixerDefinition>();
            foreach (var file in Directory.EnumerateFiles(MixersDirectory, "*.json"))
            {
                var json = await File.ReadAllTextAsync(file, ct);
                mixers.Add(serializer.DeserializeMixer(json));
            }

            return mixers;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveMixerAsync(MixerDefinition mixer, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(MixersDirectory);
            var json = serializer.SerializeMixer(mixer);
            await File.WriteAllTextAsync(Path.Combine(MixersDirectory, $"{mixer.Id}.json"), json, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteMixerAsync(Guid mixerId, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var path = Path.Combine(MixersDirectory, $"{mixerId}.json");
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<IReadOnlyList<ColorPreset>> LoadColorPresetsAsync(CancellationToken ct = default) =>
        LoadListAsync<ColorPreset>(ColorPresetsFile, ct);

    public Task SaveColorPresetsAsync(IReadOnlyList<ColorPreset> presets, CancellationToken ct = default) =>
        SaveListAsync(ColorPresetsFile, presets, ct);

    public Task<IReadOnlyList<ControlTemplate>> LoadTemplatesAsync(CancellationToken ct = default) =>
        LoadListAsync<ControlTemplate>(TemplatesFile, ct);

    public Task SaveTemplatesAsync(IReadOnlyList<ControlTemplate> templates, CancellationToken ct = default) =>
        SaveListAsync(TemplatesFile, templates, ct);

    private async Task<IReadOnlyList<T>> LoadListAsync<T>(string path, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (!File.Exists(path))
            {
                return [];
            }

            var json = await File.ReadAllTextAsync(path, ct);
            return serializer.Deserialize<List<T>>(json) ?? [];
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task SaveListAsync<T>(string path, IReadOnlyList<T> items, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(_root);
            await File.WriteAllTextAsync(path, serializer.Serialize(items), ct);
        }
        finally
        {
            _gate.Release();
        }
    }
}
