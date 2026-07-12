using MudBlazor;
using wledBridge.VirtualMixer.Models;
using wledBridge.VirtualMixer.State;

namespace wledBridge.VirtualMixer.Controls;

// Size rules per decision Q-012 (logical pixels, min - default - max; knob locked square).

internal sealed class ButtonDescriptor : IControlDescriptor
{
    public string TypeKey => BuiltInControlTypes.Button;
    public string DisplayName => "Button";
    public string Icon => Icons.Material.Filled.SmartButton;
    public ControlSizeRules SizeRules { get; } = new(32, 32, 320, 320, 64, 64);
    public Type RendererComponent => typeof(ButtonRenderer);
    public Type SettingsEditorComponent => typeof(ButtonSettingsEditor);
    public Type SettingsClrType => typeof(ButtonSettings);

    public ControlSettingsBase CreateDefaultSettings() => new ButtonSettings();

    public ControlStateBase CreateInitialState(ControlInstance instance) =>
        new ButtonState { ControlId = instance.Id };
}

internal sealed class FaderDescriptor : IControlDescriptor
{
    public string TypeKey => BuiltInControlTypes.Fader;
    public string DisplayName => "Fader";
    public string Icon => Icons.Material.Filled.Tune;
    public ControlSizeRules SizeRules { get; } = new(32, 32, 1200, 1200, 48, 200);
    public Type RendererComponent => typeof(FaderRenderer);
    public Type SettingsEditorComponent => typeof(FaderSettingsEditor);
    public Type SettingsClrType => typeof(FaderSettings);

    public ControlSettingsBase CreateDefaultSettings() => new FaderSettings();

    public ControlStateBase CreateInitialState(ControlInstance instance) =>
        new ValueState { ControlId = instance.Id };
}

internal sealed class StatusbarDescriptor : IControlDescriptor
{
    public string TypeKey => BuiltInControlTypes.Statusbar;
    public string DisplayName => "Statusbar";
    public string Icon => Icons.Material.Filled.LinearScale;
    public ControlSizeRules SizeRules { get; } = new(16, 16, 1200, 200, 128, 24);
    public Type RendererComponent => typeof(StatusbarRenderer);
    public Type SettingsEditorComponent => typeof(StatusbarSettingsEditor);
    public Type SettingsClrType => typeof(StatusbarSettings);

    public ControlSettingsBase CreateDefaultSettings() => new StatusbarSettings();

    public ControlStateBase CreateInitialState(ControlInstance instance) =>
        new ValueState { ControlId = instance.Id };
}

internal sealed class KnobDescriptor : IControlDescriptor
{
    public string TypeKey => BuiltInControlTypes.Knob;
    public string DisplayName => "Regler";
    public string Icon => Icons.Material.Filled.RadioButtonChecked;
    public ControlSizeRules SizeRules { get; } = new(40, 40, 200, 200, 64, 64, LockSquare: true);
    public Type RendererComponent => typeof(KnobRenderer);
    public Type SettingsEditorComponent => typeof(KnobSettingsEditor);
    public Type SettingsClrType => typeof(KnobSettings);

    public ControlSettingsBase CreateDefaultSettings() => new KnobSettings();

    public ControlStateBase CreateInitialState(ControlInstance instance) =>
        new ValueState { ControlId = instance.Id };
}

internal sealed class KnobBankDescriptor : IControlDescriptor
{
    public string TypeKey => BuiltInControlTypes.KnobBank;
    public string DisplayName => "Reglergruppe";
    public string Icon => Icons.Material.Filled.Apps;
    public ControlSizeRules SizeRules { get; } = new(96, 96, 1200, 1200, 240, 160);
    public Type RendererComponent => typeof(KnobBankRenderer);
    public Type SettingsEditorComponent => typeof(KnobBankSettingsEditor);
    public Type SettingsClrType => typeof(KnobBankSettings);

    public ControlSettingsBase CreateDefaultSettings()
    {
        var settings = new KnobBankSettings();
        settings.EnsureKnobItems();
        return settings;
    }

    public ControlStateBase CreateInitialState(ControlInstance instance) =>
        new SubValueState
        {
            ControlId = instance.Id,
            Values = new int[Math.Max(1, ((KnobBankSettings)instance.Settings).Count)]
        };
}

internal sealed class LineDescriptor : IControlDescriptor
{
    public string TypeKey => BuiltInControlTypes.Line;
    public string DisplayName => "Linie";
    public string Icon => Icons.Material.Filled.HorizontalRule;
    public ControlSizeRules SizeRules { get; } = new(4, 4, 1200, 1200, 160, 8);
    public Type RendererComponent => typeof(LineRenderer);
    public Type SettingsEditorComponent => typeof(LineSettingsEditor);
    public Type SettingsClrType => typeof(LineSettings);

    public ControlSettingsBase CreateDefaultSettings() => new LineSettings();

    public ControlStateBase CreateInitialState(ControlInstance instance) =>
        new ValueState { ControlId = instance.Id };
}

internal sealed class LabelDescriptor : IControlDescriptor
{
    public string TypeKey => BuiltInControlTypes.Label;
    public string DisplayName => "Label";
    public string Icon => Icons.Material.Filled.TextFields;
    public ControlSizeRules SizeRules { get; } = new(20, 14, 1200, 400, 120, 28);
    public Type RendererComponent => typeof(LabelRenderer);
    public Type SettingsEditorComponent => typeof(LabelSettingsEditor);
    public Type SettingsClrType => typeof(LabelSettings);

    public ControlSettingsBase CreateDefaultSettings() => new LabelSettings();

    public ControlStateBase CreateInitialState(ControlInstance instance) =>
        new ValueState { ControlId = instance.Id };
}
