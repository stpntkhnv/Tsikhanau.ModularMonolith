using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModularMonolith.Shared.Collections;

namespace ModularMonolith.BuildingBlocks.Api.Modules;

public static class ModuleHostExtensions
{
    private const String ModulesSection = "ModularMonolith:Modules";

    public static IHostApplicationBuilder AddModule<TModule>(this IHostApplicationBuilder builder)
        where TModule : IModule, new()
    {
        var module = new TModule();
        module.Register(builder.Services, builder.Configuration.GetSection($"{ModulesSection}:{module.Name}"));
        builder.Services.AddSingleton<IModule>(module);
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
