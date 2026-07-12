using wledBridge.VirtualMixer.Controls;
using wledBridge.VirtualMixer.Models;
using wledBridge.VirtualMixer.Persistence;
using wledBridge.VirtualMixer.Persistence.Serialization;
using wledBridge.VirtualMixer.State;

namespace wledBridge.VirtualMixer.Tests;

/// <summary>Minimal descriptor for logic tests (renderer/editor types are never instantiated).</summary>
internal sealed class FakeDescriptor(string typeKey, Type settingsType) : IControlDescriptor
{
    public string TypeKey => typeKey;
    public string DisplayName => typeKey;
    public string Icon => "";
    public ControlSizeRules SizeRules { get; } = new(10, 10, 1000, 1000, 50, 50);
    public Type RendererComponent => typeof(object);
    public Type SettingsEditorComponent => typeof(object);
    public Type SettingsClrType => settingsType;

    public ControlSettingsBase CreateDefaultSettings() =>
        (ControlSettingsBase)Activator.CreateInstance(settingsType)!;

    public ControlStateBase CreateInitialState(ControlInstance instance) => instance.Settings switch
    {
        ButtonSettings => new ButtonState { ControlId = instance.Id },
        KnobBankSettings bank => new SubValueState { ControlId = instance.Id, Values = new int[bank.Count] },
        _ => new ValueState { ControlId = instance.Id }
    };
}

internal sealed class InMemoryStore : IVirtualMixerStore
{
    public Dictionary<Guid, string> MixerJson { get; } = [];
    public List<ColorPreset> Presets { get; private set; } = [];
    public List<ControlTemplate> Templates { get; private set; } = [];

    private readonly MixerJsonSerializer _serializer;

    public InMemoryStore(MixerJsonSerializer serializer) => _serializer = serializer;

    public Task<IReadOnlyList<MixerDefinition>> LoadMixersAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<MixerDefinition>>([.. MixerJson.Values.Select(_serializer.DeserializeMixer)]);

    public Task SaveMixerAsync(MixerDefinition mixer, CancellationToken ct = default)
    {
        MixerJson[mixer.Id] = _serializer.SerializeMixer(mixer);
        return Task.CompletedTask;
    }

    public Task DeleteMixerAsync(Guid mixerId, CancellationToken ct = default)
    {
        MixerJson.Remove(mixerId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ColorPreset>> LoadColorPresetsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ColorPreset>>([.. Presets]);

    public Task SaveColorPresetsAsync(IReadOnlyList<ColorPreset> presets, CancellationToken ct = default)
    {
        Presets = [.. presets];
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ControlTemplate>> LoadTemplatesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ControlTemplate>>([.. Templates]);

    public Task SaveTemplatesAsync(IReadOnlyList<ControlTemplate> templates, CancellationToken ct = default)
    {
        Templates = [.. templates];
        return Task.CompletedTask;
    }
}

internal static class TestSetup
{
    public static ControlRegistry CreateRegistry() => new(
    [
        new FakeDescriptor(BuiltInControlTypes.Button, typeof(ButtonSettings)),
        new FakeDescriptor(BuiltInControlTypes.Fader, typeof(FaderSettings)),
        new FakeDescriptor(BuiltInControlTypes.Statusbar, typeof(StatusbarSettings)),
        new FakeDescriptor(BuiltInControlTypes.Knob, typeof(KnobSettings)),
        new FakeDescriptor(BuiltInControlTypes.KnobBank, typeof(KnobBankSettings))
    ]);

    public static MixerJsonSerializer CreateSerializer(IEnumerable<IDefinitionMigration>? migrations = null) =>
        new(CreateRegistry(), migrations ?? []);

    public static ControlInstance CreateControl(string typeKey, ControlSettingsBase settings, string name = "Test") =>
        new()
        {
            TypeKey = typeKey,
            Name = name,
            X = 10,
            Y = 10,
            Width = 60,
            Height = 60,
            Settings = settings
        };

    public static MixerDefinition CreateMixer(params ControlInstance[] controls)
    {
        var mixer = new MixerDefinition { Name = "Testmixer" };
        var group = new MixerGroup { Label = "Standard", Order = 0, Width = 1200 };
        group.Controls.AddRange(controls);
        mixer.Groups.Add(group);
        return mixer;
    }

    /// <summary>Waits for the runtime engine's async consumer to reach an expected condition.</summary>
    public static async Task WaitForAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        var start = Environment.TickCount64;
        while (!condition())
        {
            if (Environment.TickCount64 - start > timeoutMs)
            {
                throw new TimeoutException("Condition not reached within timeout.");
            }

            await Task.Delay(10);
        }
    }
}
