using wledBridge.VirtualMixer.Persistence.Serialization;

namespace wledBridge.VirtualMixer.Controls;

/// <summary>
/// Lookup for all registered control types (built-ins + host-registered). Also acts as the
/// settings-type resolver for polymorphic serialization.
/// </summary>
public sealed class ControlRegistry : ISettingsTypeResolver
{
    private readonly Dictionary<string, IControlDescriptor> _descriptors;

    public ControlRegistry(IEnumerable<IControlDescriptor> descriptors)
    {
        _descriptors = new Dictionary<string, IControlDescriptor>(StringComparer.OrdinalIgnoreCase);

        foreach (var descriptor in descriptors)
        {
            if (!_descriptors.TryAdd(descriptor.TypeKey, descriptor))
            {
                throw new InvalidOperationException($"Duplicate control type key '{descriptor.TypeKey}'.");
            }
        }
    }

    public IReadOnlyCollection<IControlDescriptor> All => _descriptors.Values;

    public IControlDescriptor Get(string typeKey) =>
        _descriptors.TryGetValue(typeKey, out var descriptor)
            ? descriptor
            : throw new KeyNotFoundException($"No control type registered for key '{typeKey}'.");

    public IControlDescriptor? Find(string typeKey) =>
        _descriptors.GetValueOrDefault(typeKey);

    public Type? ResolveSettingsType(string typeKey) => Find(typeKey)?.SettingsClrType;
}
