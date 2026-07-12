namespace wledBridge.VirtualMixer.Persistence.Serialization;

/// <summary>
/// Resolves a control type key to its settings CLR type for polymorphic (de)serialization.
/// Implemented by the control registry, so host-registered custom control types serialize
/// without any module changes.
/// </summary>
public interface ISettingsTypeResolver
{
    Type? ResolveSettingsType(string typeKey);
}
