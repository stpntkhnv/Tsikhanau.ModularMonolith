using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.OpenApi;
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
            problemDetailsOptions =>
                problemDetailsOptions.CustomizeProblemDetails = ServerErrorProblemDetails.Customize);

        builder.Services.ConfigureHttpJsonOptions(ReadAndWriteOptionals);
        builder.Services.AddOpenApi(DescribeOptionals);

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

    private static void ReadAndWriteOptionals(JsonOptions jsonOptions) =>
        jsonOptions.SerializerOptions.Converters.Add(new OptionalJsonConverterFactory());

    private static void DescribeOptionals(OpenApiOptions openApiOptions)
    {
        openApiOptions.CreateSchemaReferenceId = OptionalSchemaTransformer.CreateSchemaReferenceId;
        openApiOptions.AddSchemaTransformer<OptionalSchemaTransformer>();
    }
}
