using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tsikhanau.Monolith.Api.Callers;
using Tsikhanau.Monolith.Application.Settings;
using Tsikhanau.Monolith.Shared.Collections;

namespace Tsikhanau.Monolith.Api.Modules;

public static class ModuleHostExtensions
{
    private const String ModulesSection = $"{SettingsRoot.Section}:Modules";

    public static IHostApplicationBuilder AddModule<TModule>(this IHostApplicationBuilder builder)
        where TModule : IModule, new()
    {
        var module = new TModule();
        module.Register(builder.Services, builder.Configuration.GetSection($"{ModulesSection}:{module.Name}"));
        builder.Services
            .AddSingleton<IModule>(module)
            .AddCaller();

        return builder;
    }

    public static IApplicationBuilder UseModules(this IApplicationBuilder app)
    {
        app.ApplicationServices
            .GetServices<IModule>()
            .ForEach(module => module.UsePipeline(app));

        return app;
    }

    public static IEndpointRouteBuilder MapModules(this IEndpointRouteBuilder endpoints)
    {
        endpoints.ServiceProvider
            .GetServices<IModule>()
            .ForEach(module => module.MapEndpoints(endpoints));

        return endpoints;
    }
}
