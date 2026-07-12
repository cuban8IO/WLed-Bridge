using Microsoft.Extensions.DependencyInjection;
using wledBridge.VirtualMixer.Controls;
using wledBridge.VirtualMixer.Persistence;

namespace wledBridge.VirtualMixer;

public class VirtualMixerOptions
{
    /// <summary>Directory for the default JSON file store.</summary>
    public string StoragePath { get; set; } = Path.Combine(AppContext.BaseDirectory, "virtualmixer-data");

    /// <summary>Ring buffer capacity of the designer's event monitor.</summary>
    public int EventMonitorCapacity { get; set; } = 500;

    internal List<Type> AdditionalControlTypes { get; } = [];
    internal Type? StoreType { get; private set; }

    /// <summary>Registers a custom control type in addition to the built-ins.</summary>
    public void AddControlType<TDescriptor>() where TDescriptor : class, IControlDescriptor =>
        AdditionalControlTypes.Add(typeof(TDescriptor));

    /// <summary>Replaces the default JSON file store with a host-provided implementation.</summary>
    public void UseStore<TStore>() where TStore : class, IVirtualMixerStore =>
        StoreType = typeof(TStore);
}
