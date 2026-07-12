using System.Text.Json.Nodes;
using wledBridge.VirtualMixer.Controls;
using wledBridge.VirtualMixer.Models;
using wledBridge.VirtualMixer.Persistence.Serialization;
using Xunit;

namespace wledBridge.VirtualMixer.Tests;

public class SerializationTests
{
    [Fact]
    public void Mixer_with_all_builtin_settings_roundtrips()
    {
        var serializer = TestSetup.CreateSerializer();
        var mixer = TestSetup.CreateMixer(
            TestSetup.CreateControl(BuiltInControlTypes.Button, new ButtonSettings
            {
                Mode = ButtonMode.Toggle,
                Led = new LedConfig { ColorMode = LedColorMode.Rgb, ColorHex = "#FF0000" }
            }),
            TestSetup.CreateControl(BuiltInControlTypes.Fader, new FaderSettings
            {
                Orientation = ControlOrientation.Horizontal,
                Curve = new Curve { Type = CurveType.Exponential, Gamma = 3.0 }
            }),
            TestSetup.CreateControl(BuiltInControlTypes.Statusbar, new StatusbarSettings { ColorMode = LedColorMode.Rgb, ColorHex = "#00FF00" }),
            TestSetup.CreateControl(BuiltInControlTypes.Knob, new KnobSettings { Mode = KnobMode.Relative }),
            TestSetup.CreateControl(BuiltInControlTypes.KnobBank, new KnobBankSettings { Count = 6, Layout = KnobBankLayout.FixedColumns, FixedColumns = 3 }));

        mixer.Bindings.Add(new ControlBinding
        {
            SourceControlId = mixer.AllControls.First().Id,
            TargetControlId = mixer.AllControls.Last().Id,
            TargetSubIndex = 2,
            Transform = new BindingTransform { Type = BindingTransformType.Invert }
        });

        var json = serializer.SerializeMixer(mixer);
        var restored = serializer.DeserializeMixer(json);

        Assert.Equal(mixer.Id, restored.Id);
        Assert.Equal(5, restored.AllControls.Count());
        Assert.Single(restored.Bindings);

        var button = restored.AllControls.First(c => c.TypeKey == BuiltInControlTypes.Button);
        var buttonSettings = Assert.IsType<ButtonSettings>(button.Settings);
        Assert.Equal(ButtonMode.Toggle, buttonSettings.Mode);
        Assert.Equal("#FF0000", buttonSettings.Led?.ColorHex);

        var fader = restored.AllControls.First(c => c.TypeKey == BuiltInControlTypes.Fader);
        var faderSettings = Assert.IsType<FaderSettings>(fader.Settings);
        Assert.Equal(CurveType.Exponential, faderSettings.Curve.Type);
        Assert.Equal(3.0, faderSettings.Curve.Gamma);

        var knob = restored.AllControls.First(c => c.TypeKey == BuiltInControlTypes.Knob);
        Assert.Equal(KnobMode.Relative, Assert.IsType<KnobSettings>(knob.Settings).Mode);

        var bank = restored.AllControls.First(c => c.TypeKey == BuiltInControlTypes.KnobBank);
        Assert.Equal(6, Assert.IsType<KnobBankSettings>(bank.Settings).Count);
    }

    [Fact]
    public void Unknown_type_key_throws_clear_error()
    {
        var serializer = TestSetup.CreateSerializer();
        var json = """
            {
              "Name": "M",
              "SchemaVersion": 1,
              "Groups": [ { "Label": "G", "Controls": [ { "TypeKey": "does-not-exist", "Name": "X", "Settings": {} } ] } ]
            }
            """;

        var ex = Assert.ThrowsAny<Exception>(() => serializer.DeserializeMixer(json));
        Assert.Contains("does-not-exist", ex.Message);
    }

    private sealed class DummyMigrationV0 : IDefinitionMigration
    {
        public int FromVersion => 0;

        public void Migrate(JsonObject root) => root["Name"] = root["Name"]!.GetValue<string>() + " (migriert)";
    }

    [Fact]
    public void Migration_chain_upgrades_older_schema_versions()
    {
        var serializer = TestSetup.CreateSerializer([new DummyMigrationV0()]);
        var json = """
            { "Name": "Alt", "SchemaVersion": 0, "Groups": [] }
            """;

        var mixer = serializer.DeserializeMixer(json);

        Assert.Equal("Alt (migriert)", mixer.Name);
        Assert.Equal(MixerDefinition.CurrentSchemaVersion, mixer.SchemaVersion);
    }

    [Fact]
    public void Newer_schema_version_is_rejected()
    {
        var serializer = TestSetup.CreateSerializer();
        var json = $$"""
            { "Name": "Zukunft", "SchemaVersion": {{MixerDefinition.CurrentSchemaVersion + 1}}, "Groups": [] }
            """;

        Assert.ThrowsAny<Exception>(() => serializer.DeserializeMixer(json));
    }

    [Fact]
    public void Clone_produces_independent_copy()
    {
        var serializer = TestSetup.CreateSerializer();
        var mixer = TestSetup.CreateMixer(
            TestSetup.CreateControl(BuiltInControlTypes.Button, new ButtonSettings()));

        var clone = serializer.Clone(mixer);
        clone.Groups[0].Controls[0].Name = "geändert";

        Assert.Equal("Test", mixer.Groups[0].Controls[0].Name);
        Assert.Equal("geändert", clone.Groups[0].Controls[0].Name);
    }
}
