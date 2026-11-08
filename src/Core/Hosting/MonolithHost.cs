using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Tsikhanau.Monolith.Hosting.CommandLine;
using Tsikhanau.Monolith.Hosting.Observability;
using Tsikhanau.Monolith.Infrastructure.Database.Settings;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Monolith.Infrastructure.Platform;
using Tsikhanau.Monolith.Infrastructure.Queues.CommandLine;

namespace Tsikhanau.Monolith.Hosting;

public static class MonolithHost
{
    private const Int32 Success = 0;

    public static Task<Int32> RunAsync(String[] args, Action<WebApplicationBuilder> addServices) =>
        args is [QueuesTool.Name, .. var queuesArguments]
            ? QueuesCommandHost.HostQueuesToolAsync(queuesArguments, addServices)
            : HostWebAppAsync(args, addServices);

    private static async Task<Int32> HostWebAppAsync(String[] args, Action<WebApplicationBuilder> addServices)
    {
        await using var app = BuildApp(args, addServices);
        try
        {
            await RunAppAsync(app);

            return Success;
        }
        catch (Exception exception)
        {
            HostLog.Terminated(app.Logger, exception);
            throw;
        }
    }

    private static async Task RunAppAsync(WebApplication app)
    {
        var settings = app.Services.ValidateSettings();
        await app.Services.PrepareOrVerifyModuleDatabasesAsync(settings, CancellationToken.None);
        if (settings.PrepareMode == DatabasePrepareMode.Only)
        {
            HostLog.PreparedOnly(app.Logger);
            return;
        }

        await ServeAsync(app);
    }

    private static WebApplication BuildApp(String[] args, Action<WebApplicationBuilder> addServices)
    {
        try
        {
            return CreateBuilder(args, addServices)
                .Build();
        }
        catch (Exception exception) when (exception is not HostAbortedException)
        {
            HostLog.TerminatedBeforeTheHostWasBuilt(exception);
            throw;
        }
    }

    internal static WebApplicationBuilder CreateBuilder(String[] args, Action<WebApplicationBuilder> addServices)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddTelemetryAndHttpDefaults();
        builder.Services.AddPlatformServices(builder.Configuration);
        addServices(builder);
        builder.EnsureConnectionString();
        builder.AddOtlpExporterWhenConfigured();
        return builder;
    }

    private static async Task ServeAsync(WebApplication app)
    {
        app.UseRequestPipeline();
        await app.StartAsync();
        await app.WaitForShutdownAsync();
    }
}
