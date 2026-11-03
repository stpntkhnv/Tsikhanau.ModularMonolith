using Microsoft.Extensions.DependencyInjection;

namespace Tsikhanau.Monolith.Infrastructure.Modules;

internal static class ServiceCollectionInstanceExtensions
{
    public static T GetOrAddInstance<T>(this IServiceCollection services)
        where T : class, new()
    {
        var existingInstance = services
            .Where(descriptor => !descriptor.IsKeyedService && descriptor.ServiceType == typeof(T))
            .Select(descriptor => descriptor.ImplementationInstance)
            .OfType<T>()
            .FirstOrDefault();

        if (existingInstance is not null)
        {
            return existingInstance;
        }

        var createdInstance = new T();
        services.AddSingleton(createdInstance);
        return createdInstance;
    }

    public static Boolean TryAddMarker<TMarker>(this IServiceCollection services)
        where TMarker : class, new()
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(TMarker)))
        {
            return false;
        }

        services.AddSingleton(new TMarker());
        return true;
    }
}
