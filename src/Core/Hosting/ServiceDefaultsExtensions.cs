using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Scalar.AspNetCore;
using Tsikhanau.Monolith.Contracts.Json;
using Tsikhanau.Monolith.Hosting.Observability;
using Tsikhanau.Monolith.Hosting.OpenApi;
using Tsikhanau.Monolith.Hosting.Problems;
using Tsikhanau.Monolith.Infrastructure.DependencyInjection;

namespace Tsikhanau.Monolith.Hosting;

public static class ServiceDefaultsExtensions
{
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(25);

    public static WebApplicationBuilder AddServiceDefaults(
        this WebApplicationBuilder builder,
        params String[] activitySources)
    {
        builder.AddTelemetryAndHttpDefaults(activitySources);
        builder.AddOtlpExporterWhenConfigured();
        return builder;
    }

    public static WebApplicationBuilder AddHttpDefaults(this WebApplicationBuilder builder)
    {
        if (builder.Services.TryAddMarker<HttpDefaultsMarker>())
        {
            builder.AddHealth();
            builder.Services
                .SetShutdownTimeout()
                .AddProblemResponses()
                .AddOptionalJson()
                .AddOpenApiDocument();
        }

        return builder;
    }

    public static WebApplication UseServiceDefaults(this WebApplication app)
    {
        app.UseProblemResponses();
        app.UseCorsWhenRegistered();
        app.UseAuthenticationAndAuthorizationWhenRegistered();
        app.MapHealth();
        app.MapApiReferenceInDevelopment();
        return app;
    }

    internal static WebApplicationBuilder AddTelemetryAndHttpDefaults(
        this WebApplicationBuilder builder,
        params String[] activitySources)
    {
        builder.AddTelemetry(activitySources);
        return builder.AddHttpDefaults();
    }

    private static IServiceCollection SetShutdownTimeout(this IServiceCollection services) =>
        services.Configure<HostOptions>(hostOptions => hostOptions.ShutdownTimeout = ShutdownTimeout);

    private static IServiceCollection AddProblemResponses(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.PostConfigure<ProblemDetailsOptions>(KeepTheCoreProblemShape);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IProblemDetailsWriter, AnyAcceptProblemDetailsWriter>());
        return services.Configure<RouteHandlerOptions>(
            routeHandlerOptions => routeHandlerOptions.ThrowOnBadRequest = true);
    }

    private static IServiceCollection AddOptionalJson(this IServiceCollection services) =>
        services.ConfigureHttpJsonOptions(ReadAndWriteOptionals);

    private static IServiceCollection AddOpenApiDocument(this IServiceCollection services) =>
        services.AddOpenApi(DescribeOptionalsAndProblems);

    private static void UseProblemResponses(this WebApplication app)
    {
        app.UseExceptionHandler(
            new ExceptionHandlerOptions
            {
                StatusCodeSelector = BadRequestProblemDetails.SelectStatusCode,
                SuppressDiagnosticsCallback = BadRequestProblemDetails.IsBadRequest
            });

        app.UseStatusCodePages();
    }

    private static void UseCorsWhenRegistered(this WebApplication app)
    {
        if (app.IsRegistered<ICorsService>())
        {
            app.UseCors();
        }
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

    private static void MapApiReferenceInDevelopment(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app
                .MapOpenApi()
                .AllowAnonymous();

            app
                .MapScalarApiReference()
                .AllowAnonymous();
        }
    }

    private static Boolean IsRegistered<TService>(this WebApplication app) =>
        app.Services
            .GetRequiredService<IServiceProviderIsService>()
            .IsService(typeof(TService));

    private static void KeepTheCoreProblemShape(ProblemDetailsOptions problemDetailsOptions) =>
        problemDetailsOptions.CustomizeProblemDetails += CustomizeProblemDetails;

    private static void CustomizeProblemDetails(ProblemDetailsContext context)
    {
        BadRequestProblemDetails.Customize(context);
        ServerErrorProblemDetails.Customize(context);
    }

    private static void ReadAndWriteOptionals(JsonOptions jsonOptions) =>
        jsonOptions.SerializerOptions.Converters.Add(new OptionalJsonConverterFactory());

    private static void DescribeOptionalsAndProblems(OpenApiOptions openApiOptions)
    {
        openApiOptions.CreateSchemaReferenceId = OptionalSchemaTransformer.CreateSchemaReferenceId;
        openApiOptions.AddSchemaTransformer<OptionalSchemaTransformer>();
        openApiOptions.AddSchemaTransformer<ProblemDetailsSchemaTransformer>();
    }
}

file sealed class HttpDefaultsMarker;
