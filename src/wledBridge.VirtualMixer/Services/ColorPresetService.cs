using wledBridge.VirtualMixer.Models;
using wledBridge.VirtualMixer.Persistence;

namespace wledBridge.VirtualMixer.Services;

internal sealed class ColorPresetService(IVirtualMixerStore store) : IColorPresetService
{
    private const int MaxRecentColors = 8;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<string> _recent = [];
    private List<ColorPreset>? _presets;

    public event EventHandler? Changed;

    public IReadOnlyList<string> RecentColors
    {
        get
        {
            lock (_recent)
            {
                return [.. _recent];
            }
        }
    }

    public void PushRecentColor(string colorHex)
    {
        if (string.IsNullOrWhiteSpace(colorHex))
        {
            return;
        }

        lock (_recent)
        {
            _recent.RemoveAll(c => string.Equals(c, colorHex, StringComparison.OrdinalIgnoreCase));
            _recent.Insert(0, colorHex);
            if (_recent.Count > MaxRecentColors)
            {
                _recent.RemoveRange(MaxRecentColors, _recent.Count - MaxRecentColors);
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task<IReadOnlyList<ColorPreset>> GetPresetsAsync()
    {
        var presets = await EnsureLoadedAsync();
        return [.. presets.OrderByDescending(p => p.IsFavorite).ThenBy(p => p.Order).ThenBy(p => p.Name)];
    }

    public async Task SavePresetAsync(ColorPreset preset)
    {
        var presets = await EnsureLoadedAsync();
        await _gate.WaitAsync();
        try
        {
            var index = presets.FindIndex(p => p.Id == preset.Id);
            if (index >= 0)
            {
                presets[index] = preset;
            }
            else
            {
                presets.Add(preset);
            }

            await store.SaveColorPresetsAsync(presets);
        }
        finally
        {
            _gate.Release();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task DeletePresetAsync(Guid id)
    {
        var presets = await EnsureLoadedAsync();
        await _gate.WaitAsync();
        try
        {
            if (presets.RemoveAll(p => p.Id == id) > 0)
            {
                await store.SaveColorPresetsAsync(presets);
            }
        }
        finally
        {
            _gate.Release();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task<List<ColorPreset>> EnsureLoadedAsync()
    {
        if (_presets is not null)
        {
            return _presets;
        }

        await _gate.WaitAsync();
        try
        {
            _presets ??= [.. await store.LoadColorPresetsAsync()];
            return _presets;
        }
        finally
        {
            _gate.Release();
        }
    }
}
