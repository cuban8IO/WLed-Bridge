using Microsoft.Extensions.DependencyInjection;

namespace wledBridge.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services;
    }
}
