using Microsoft.AspNetCore.Builder;
using Tsikhanau.Monolith.Api.Modules;

namespace Tsikhanau.Monolith.Hosting;

public static class RequestPipelineExtensions
{
    public static WebApplication UseRequestPipeline(this WebApplication app)
    {
        app.UseServiceDefaults();
        app.UseModules();
        app.MapModules();
        return app;
    }
}
