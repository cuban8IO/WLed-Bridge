using System.Text.Json.Nodes;

namespace wledBridge.VirtualMixer.Persistence.Serialization;

/// <summary>
/// One step of the mixer-definition schema migration chain. Migrations operate on the raw JSON
/// tree (before deserialization) and are applied in ascending <see cref="FromVersion"/> order
/// until the document reaches <see cref="Models.MixerDefinition.CurrentSchemaVersion"/>.
/// </summary>
public interface IDefinitionMigration
{
    /// <summary>The schema version this migration upgrades FROM (to FromVersion + 1).</summary>
    int FromVersion { get; }

    void Migrate(JsonObject root);
}
