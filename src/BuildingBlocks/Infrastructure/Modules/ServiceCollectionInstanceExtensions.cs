using Microsoft.Extensions.DependencyInjection;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Modules;

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
}
