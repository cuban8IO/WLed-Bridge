using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using wledBridge.Application.Abstractions;
using wledBridge.Application.Abstractions.Controllers;
using wledBridge.Application.Abstractions.Midi;
using wledBridge.Application.Abstractions.Persistence;
using wledBridge.Infrastructure.Controllers;
using wledBridge.Infrastructure.Midi;
using wledBridge.Infrastructure.Persistence;
using wledBridge.Infrastructure.Wled;

namespace wledBridge.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<IWledClient, WledHttpClient>();
        services.AddSingleton<IMidiPortFactory, NAudioMidiPortFactory>();
        services.AddSingleton<IControllerDriverFactory, ControllerDriverFactory>();

        var connectionString = configuration.GetConnectionString("WledBridge") ?? "Data Source=wledbridge.db";
        services.AddDbContext<WledBridgeDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<WledBridgeDbContext>());

        return services;
    }
}
