using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using wledBridge.VirtualMixer.Models;

namespace wledBridge.VirtualMixer.Persistence.Serialization;

/// <summary>
/// Central (de)serializer for everything the module persists. Control settings are polymorphic:
/// the discriminator is the owning instance's/template's TypeKey, resolved through
/// <see cref="ISettingsTypeResolver"/>. Mixer definitions carry a SchemaVersion and are run
/// through the migration chain before deserialization.
/// </summary>
public sealed class MixerJsonSerializer
{
    private readonly ISettingsTypeResolver _resolver;
    private readonly IReadOnlyList<IDefinitionMigration> _migrations;
    private readonly JsonSerializerOptions _options;

    public MixerJsonSerializer(ISettingsTypeResolver resolver, IEnumerable<IDefinitionMigration> migrations)
    {
        _resolver = resolver;
        _migrations = [.. migrations.OrderBy(m => m.FromVersion)];
        _options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters =
            {
                new JsonStringEnumConverter(),
                new ControlInstanceJsonConverter(resolver),
                new ControlTemplateJsonConverter(resolver)
            }
        };
    }

    public JsonSerializerOptions Options => _options;

    public string SerializeMixer(MixerDefinition mixer) => JsonSerializer.Serialize(mixer, _options);

    public MixerDefinition DeserializeMixer(string json)
    {
        var root = JsonNode.Parse(json) as JsonObject
            ?? throw new JsonException("Mixer definition is not a JSON object.");

        var version = root["SchemaVersion"]?.GetValue<int>() ?? 1;

        if (version > MixerDefinition.CurrentSchemaVersion)
        {
            throw new JsonException(
                $"Mixer definition has schema version {version}, but this module only supports up to {MixerDefinition.CurrentSchemaVersion}.");
        }

        while (version < MixerDefinition.CurrentSchemaVersion)
        {
            var migration = _migrations.FirstOrDefault(m => m.FromVersion == version)
                ?? throw new JsonException($"No migration registered for schema version {version}.");
            migration.Migrate(root);
            version++;
            root["SchemaVersion"] = version;
        }

        return root.Deserialize<MixerDefinition>(_options)
            ?? throw new JsonException("Mixer definition deserialized to null.");
    }

    public string Serialize<T>(T value) => JsonSerializer.Serialize(value, _options);

    public T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, _options);

    /// <summary>Deep-clones any serializable object (used by designer working copies and clipboard).</summary>
    public T Clone<T>(T value) =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, _options), _options)
            ?? throw new InvalidOperationException("Clone round-trip produced null.");
}

/// <summary>
/// Serializes <see cref="ControlInstance"/> using its TypeKey as the discriminator for the
/// polymorphic Settings property.
/// </summary>
internal sealed class ControlInstanceJsonConverter(ISettingsTypeResolver resolver) : JsonConverter<ControlInstance>
{
    public override ControlInstance Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var node = JsonNode.Parse(ref reader) as JsonObject
            ?? throw new JsonException("Control instance is not a JSON object.");

        var typeKey = node["TypeKey"]?.GetValue<string>()
            ?? throw new JsonException("Control instance has no TypeKey.");

        var settingsType = resolver.ResolveSettingsType(typeKey)
            ?? throw new JsonException($"Unknown control type '{typeKey}' - no settings type registered.");

        var settingsNode = node["Settings"];
        var settings = settingsNode is null
            ? null
            : (ControlSettingsBase?)settingsNode.Deserialize(settingsType, options);

        return new ControlInstance
        {
            Id = node["Id"]?.GetValue<Guid>() ?? Guid.NewGuid(),
            TypeKey = typeKey,
            Name = node["Name"]?.GetValue<string>() ?? typeKey,
            Label = node["Label"]?.GetValue<string>(),
            X = node["X"]?.GetValue<int>() ?? 0,
            Y = node["Y"]?.GetValue<int>() ?? 0,
            Width = node["Width"]?.GetValue<int>() ?? 0,
            Height = node["Height"]?.GetValue<int>() ?? 0,
            BackgroundColorHex = node["BackgroundColorHex"]?.GetValue<string>(),
            IsVisible = node["IsVisible"]?.GetValue<bool>() ?? true,
            IsEnabled = node["IsEnabled"]?.GetValue<bool>() ?? true,
            Settings = settings ?? throw new JsonException($"Control instance of type '{typeKey}' has no Settings.")
        };
    }

    public override void Write(Utf8JsonWriter writer, ControlInstance value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("Id", value.Id);
        writer.WriteString("TypeKey", value.TypeKey);
        writer.WriteString("Name", value.Name);
        if (value.Label is not null) writer.WriteString("Label", value.Label);
        writer.WriteNumber("X", value.X);
        writer.WriteNumber("Y", value.Y);
        writer.WriteNumber("Width", value.Width);
        writer.WriteNumber("Height", value.Height);
        if (value.BackgroundColorHex is not null) writer.WriteString("BackgroundColorHex", value.BackgroundColorHex);
        writer.WriteBoolean("IsVisible", value.IsVisible);
        writer.WriteBoolean("IsEnabled", value.IsEnabled);
        writer.WritePropertyName("Settings");
        JsonSerializer.Serialize(writer, value.Settings, value.Settings.GetType(), options);
        writer.WriteEndObject();
    }
}

/// <summary>Same discriminator approach for <see cref="ControlTemplate"/>.</summary>
internal sealed class ControlTemplateJsonConverter(ISettingsTypeResolver resolver) : JsonConverter<ControlTemplate>
{
    public override ControlTemplate Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var node = JsonNode.Parse(ref reader) as JsonObject
            ?? throw new JsonException("Control template is not a JSON object.");

        var typeKey = node["TypeKey"]?.GetValue<string>()
            ?? throw new JsonException("Control template has no TypeKey.");

        var settingsType = resolver.ResolveSettingsType(typeKey)
            ?? throw new JsonException($"Unknown control type '{typeKey}' - no settings type registered.");

        var settings = node["Settings"] is { } settingsNode
            ? (ControlSettingsBase?)settingsNode.Deserialize(settingsType, options)
            : null;

        return new ControlTemplate
        {
            Id = node["Id"]?.GetValue<Guid>() ?? Guid.NewGuid(),
            Name = node["Name"]?.GetValue<string>() ?? typeKey,
            TypeKey = typeKey,
            Label = node["Label"]?.GetValue<string>(),
            Width = node["Width"]?.GetValue<int>() ?? 0,
            Height = node["Height"]?.GetValue<int>() ?? 0,
            BackgroundColorHex = node["BackgroundColorHex"]?.GetValue<string>(),
            Settings = settings ?? throw new JsonException($"Control template of type '{typeKey}' has no Settings.")
        };
    }

    public override void Write(Utf8JsonWriter writer, ControlTemplate value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("Id", value.Id);
        writer.WriteString("Name", value.Name);
        writer.WriteString("TypeKey", value.TypeKey);
        if (value.Label is not null) writer.WriteString("Label", value.Label);
        writer.WriteNumber("Width", value.Width);
        writer.WriteNumber("Height", value.Height);
        if (value.BackgroundColorHex is not null) writer.WriteString("BackgroundColorHex", value.BackgroundColorHex);
        writer.WritePropertyName("Settings");
        JsonSerializer.Serialize(writer, value.Settings, value.Settings.GetType(), options);
        writer.WriteEndObject();
    }
}
