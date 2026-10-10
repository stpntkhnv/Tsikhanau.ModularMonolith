using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tsikhanau.Monolith.Application.Callers;

namespace Tsikhanau.Monolith.Api.Callers;

public static class CallerServiceCollectionExtensions
{
    public static IServiceCollection AddCaller(this IServiceCollection services)
    {
        services
            .AddHttpContextAccessor()
            .TryAddSingleton<ICaller, HttpCaller>();

        return services;
    }
}
