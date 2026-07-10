using Microsoft.Extensions.DependencyInjection;
using wledBridge.Application.Controllers;

namespace wledBridge.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IControllerSessionManager, ControllerSessionManager>();

        return services;
    }
}
