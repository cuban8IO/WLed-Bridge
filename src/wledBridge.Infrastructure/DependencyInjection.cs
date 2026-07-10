using Microsoft.Extensions.DependencyInjection;
using wledBridge.Application.Abstractions;
using wledBridge.Infrastructure.Midi;
using wledBridge.Infrastructure.Wled;

namespace wledBridge.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddHttpClient<IWledClient, WledHttpClient>();
        services.AddSingleton<IMidiInputService, NullMidiInputService>();

        return services;
    }
}
