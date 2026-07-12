using wledBridge.VirtualMixer.Models;
using wledBridge.VirtualMixer.Persistence;
using wledBridge.VirtualMixer.Persistence.Serialization;

namespace wledBridge.VirtualMixer.Services;

/// <summary>Persisted, reusable control configurations shown in the designer toolbox.</summary>
internal sealed class ControlTemplateService(IVirtualMixerStore store, MixerJsonSerializer serializer)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private List<ControlTemplate>? _templates;

    public event EventHandler? Changed;

    public async Task<IReadOnlyList<ControlTemplate>> GetTemplatesAsync()
    {
        var templates = await EnsureLoadedAsync();
        return [.. templates.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)];
    }

    public async Task SaveFromControlAsync(string name, ControlInstance control)
    {
        var templates = await EnsureLoadedAsync();
        await _gate.WaitAsync();
        try
        {
            templates.Add(new ControlTemplate
            {
                Name = name,
                TypeKey = control.TypeKey,
                Label = control.Label,
                Width = control.Width,
                Height = control.Height,
                BackgroundColorHex = control.BackgroundColorHex,
                Settings = serializer.Clone(control).Settings
            });
            await store.SaveTemplatesAsync(templates);
        }
        finally
        {
            _gate.Release();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task RenameAsync(Guid id, string newName)
    {
        var templates = await EnsureLoadedAsync();
        await _gate.WaitAsync();
        try
        {
            if (templates.FirstOrDefault(t => t.Id == id) is { } template)
            {
                template.Name = newName;
                await store.SaveTemplatesAsync(templates);
            }
        }
        finally
        {
            _gate.Release();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        var templates = await EnsureLoadedAsync();
        await _gate.WaitAsync();
        try
        {
            if (templates.RemoveAll(t => t.Id == id) > 0)
            {
                await store.SaveTemplatesAsync(templates);
            }
        }
        finally
        {
            _gate.Release();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Creates a fresh, independent control instance from a template.</summary>
    public ControlInstance Instantiate(ControlTemplate template)
    {
        var clone = serializer.Clone(template);
        return new ControlInstance
        {
            TypeKey = clone.TypeKey,
            Name = clone.Name,
            Label = clone.Label,
            Width = clone.Width,
            Height = clone.Height,
            BackgroundColorHex = clone.BackgroundColorHex,
            Settings = clone.Settings
        };
    }

    private async Task<List<ControlTemplate>> EnsureLoadedAsync()
    {
        if (_templates is not null)
        {
            return _templates;
        }

        await _gate.WaitAsync();
        try
        {
            _templates ??= [.. await store.LoadTemplatesAsync()];
            return _templates;
        }
        finally
        {
            _gate.Release();
        }
    }
}
