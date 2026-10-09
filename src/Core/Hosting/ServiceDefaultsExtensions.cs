using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scalar.AspNetCore;

namespace Tsikhanau.Monolith.Hosting;

public static class ServiceDefaultsExtensions
{
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(25);

    public static WebApplicationBuilder AddServiceDefaults(
        this WebApplicationBuilder builder,
        params String[] activitySources)
    {
        builder.AddTelemetry(activitySources);
        builder.AddHealth();
        builder.Services.Configure<HostOptions>(hostOptions => hostOptions.ShutdownTimeout = ShutdownTimeout);
        builder.Services.AddProblemDetails();
        builder.Services.AddOpenApi();

        return builder;
    }

    public static WebApplication UseServiceDefaults(this WebApplication app)
    {
        app.UseExceptionHandler(
            new ExceptionHandlerOptions
            {
                StatusCodeSelector = exception => exception is BadHttpRequestException badHttpRequestException
                    ? badHttpRequestException.StatusCode
                    : StatusCodes.Status500InternalServerError
            });

        app.UseStatusCodePages();
        app.MapHealth();
        if (app.Environment.IsDevelopment())
        {
            app
                .MapOpenApi()
                .AllowAnonymous();

            app
                .MapScalarApiReference()
                .AllowAnonymous();
        }

        return app;
    }
}
