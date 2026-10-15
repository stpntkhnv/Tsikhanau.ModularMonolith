using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Tsikhanau.Monolith.Api.Modules;
using Tsikhanau.Monolith.Infrastructure.Database;
using Tsikhanau.Monolith.Infrastructure.Modules;

namespace Tsikhanau.Monolith.Hosting;

public static class MonolithHost
{
    private const Int32 Success = 0;

    public static async Task<Int32> RunAsync(String[] args, Action<WebApplicationBuilder> addServices)
    {
        await using var app = Build(args, addServices);
        try
        {
            await RunAsync(app);
            return Success;
        }
        catch (Exception exception)
        {
            HostLog.Terminated(app.Logger, exception);
            throw;
        }
    }

    private static WebApplication Build(String[] args, Action<WebApplicationBuilder> addServices)
    {
        try
        {
            var builder = WebApplication.CreateBuilder(args);

            addServices(builder);
            return builder.Build();
        }
        catch (Exception exception) when (exception is not HostAbortedException)
        {
            HostLog.TerminatedBeforeTheHostWasBuilt(exception);
            throw;
        }
    }

    private static Task RunAsync(WebApplication app)
    {
        ValidateSettings(app.Services);
        return ReadPrepareMode(app.Services) switch
        {
            DatabasePrepareMode.Only => PrepareOnlyAsync(app),
            DatabasePrepareMode.Never => VerifyAndServeAsync(app),
            _ => PrepareAndServeAsync(app),
        };
    }

    private static void ValidateSettings(IServiceProvider serviceProvider) =>
        serviceProvider
            .GetRequiredService<IStartupValidator>()
            .Validate();

    private static DatabasePrepareMode ReadPrepareMode(IServiceProvider serviceProvider) =>
        serviceProvider.GetRequiredService<IOptions<DatabaseSettings>>().Value.PrepareMode;

    private static async Task PrepareAndServeAsync(WebApplication app)
    {
        await app.Services.PrepareModuleDatabasesAsync(CancellationToken.None);
        await ServeAsync(app);
    }

    private static async Task PrepareOnlyAsync(WebApplication app)
    {
        await app.Services.PrepareModuleDatabasesOnlyAsync(CancellationToken.None);
        HostLog.PreparedOnly(app.Logger);
    }

    private static async Task VerifyAndServeAsync(WebApplication app)
    {
        await app.Services.VerifyModuleDatabasesAsync(CancellationToken.None);
        await ServeAsync(app);
    }

    private static async Task ServeAsync(WebApplication app)
    {
        app.UseServiceDefaults();
        app.UseModules();
        app.MapModules();
        await app.StartAsync();
        await app.WaitForShutdownAsync();
    }
}
