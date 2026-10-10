using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scalar.AspNetCore;
using Tsikhanau.Monolith.Contracts.Json;

namespace Tsikhanau.Monolith.Hosting;

public static class ServiceDefaultsExtensions
{
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(25);

    public static WebApplicationBuilder AddServiceDefaults(
        this WebApplicationBuilder builder,
        params String[] activitySources)
    {
        builder.AddTelemetry(activitySources);
        return builder.AddHttpDefaults();
    }

    public static WebApplicationBuilder AddHttpDefaults(this WebApplicationBuilder builder)
    {
        builder.AddHealth();
        builder.Services.Configure<HostOptions>(hostOptions => hostOptions.ShutdownTimeout = ShutdownTimeout);
        builder.Services.AddProblemDetails(
            problemDetailsOptions => problemDetailsOptions.CustomizeProblemDetails = CustomizeProblemDetails);

        builder.Services.Configure<RouteHandlerOptions>(
            routeHandlerOptions => routeHandlerOptions.ThrowOnBadRequest = true);

        builder.Services.ConfigureHttpJsonOptions(ReadAndWriteOptionals);
        builder.Services.AddOpenApi(DescribeOptionals);

        return builder;
    }

    public static WebApplication UseServiceDefaults(this WebApplication app)
    {
        app.UseExceptionHandler(
            new ExceptionHandlerOptions
            {
                StatusCodeSelector = BadRequestProblemDetails.SelectStatusCode,
                SuppressDiagnosticsCallback = BadRequestProblemDetails.IsBadRequest
            });

        app.UseStatusCodePages();
        app.UseAuthenticationAndAuthorizationWhenRegistered();
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

    private static void UseAuthenticationAndAuthorizationWhenRegistered(this WebApplication app)
    {
        if (app.IsRegistered<IAuthenticationSchemeProvider>())
        {
            app.UseAuthentication();
        }

        if (app.IsRegistered<IAuthorizationHandlerProvider>())
        {
            app.UseAuthorization();
        }
    }

    private static Boolean IsRegistered<TService>(this WebApplication app) =>
        app.Services
            .GetRequiredService<IServiceProviderIsService>()
            .IsService(typeof(TService));

    private static void CustomizeProblemDetails(ProblemDetailsContext context)
    {
        BadRequestProblemDetails.Customize(context);
        ServerErrorProblemDetails.Customize(context);
    }

    private static void ReadAndWriteOptionals(JsonOptions jsonOptions) =>
        jsonOptions.SerializerOptions.Converters.Add(new OptionalJsonConverterFactory());

    private static void DescribeOptionals(OpenApiOptions openApiOptions)
    {
        openApiOptions.CreateSchemaReferenceId = OptionalSchemaTransformer.CreateSchemaReferenceId;
        openApiOptions.AddSchemaTransformer<OptionalSchemaTransformer>();
    }
}
