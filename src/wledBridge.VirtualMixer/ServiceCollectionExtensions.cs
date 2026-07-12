using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using wledBridge.VirtualMixer.Controls;
using wledBridge.VirtualMixer.Persistence;
using wledBridge.VirtualMixer.Persistence.Serialization;
using wledBridge.VirtualMixer.Services;
using wledBridge.VirtualMixer.State;

namespace wledBridge.VirtualMixer;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Virtual Mixer module. The host only ever talks to
    /// <see cref="IVirtualMixerManager"/>, <see cref="IVirtualMixerRuntime"/> and the public
    /// components; everything else is internal.
    /// </summary>
    public static IServiceCollection AddVirtualMixer(
        this IServiceCollection services,
        Action<VirtualMixerOptions>? configure = null)
    {
        var options = new VirtualMixerOptions();
        configure?.Invoke(options);
        services.AddSingleton(Options.Create(options));

        // Control types: built-ins + host-registered.
        foreach (var descriptorType in BuiltInControlTypes.Descriptors.Concat(options.AdditionalControlTypes))
        {
            services.AddSingleton(typeof(IControlDescriptor), descriptorType);
        }

        services.AddSingleton<ControlRegistry>();
        services.AddSingleton<ISettingsTypeResolver>(sp => sp.GetRequiredService<ControlRegistry>());
        services.AddSingleton<MixerJsonSerializer>();

        // Persistence: default JSON file store, replaceable by the host.
        if (options.StoreType is { } storeType)
        {
            services.AddSingleton(typeof(IVirtualMixerStore), storeType);
        }
        else
        {
            services.AddSingleton<IVirtualMixerStore, JsonFileVirtualMixerStore>();
        }

        // Runtime engine + manager (singletons: state is shared across all circuits).
        services.AddSingleton<MixerRuntimeState>();
        services.AddSingleton<IVirtualMixerRuntime>(sp => sp.GetRequiredService<MixerRuntimeState>());
        services.AddSingleton<IVirtualMixerManager, VirtualMixerManager>();
        services.AddSingleton<EventMonitorBuffer>();
        services.AddSingleton<IColorPresetService, ColorPresetService>();
        services.AddSingleton<ControlTemplateService>();
        services.AddSingleton<DesignerClipboard>();
        services.AddSingleton<SimulationService>();

        // SignalR event bridge: dormant unless the host adds SignalR and maps the hub.
        services.TryAddSingleton<Http.VirtualMixerEventBridge>();
        services.AddHostedService(sp => sp.GetRequiredService<Http.VirtualMixerEventBridge>());

        return services;
    }
}
