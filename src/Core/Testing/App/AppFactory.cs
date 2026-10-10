using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Tsikhanau.Monolith.Testing.App;

public abstract class AppFactory<TEntryPoint>(TimeProvider timeProvider, String environmentName)
    : WebApplicationFactory<TEntryPoint>
    where TEntryPoint : class
{
    private readonly StartupFailureLoggerProvider _startupFailureLoggerProvider = new();

    internal ModuleTestSetups ModuleTestSetups { get; set; } = ModuleTestSetups.None;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environmentName);
        builder.ConfigureTestServices(ConfigureTestServices);
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        try
        {
            return base.CreateHost(builder);
        }
        catch (ObjectDisposedException) when (_startupFailureLoggerProvider.Failure.HasValue)
        {
            ExceptionDispatchInfo.Throw(_startupFailureLoggerProvider.Failure.Value);
            throw;
        }
    }

    private void ConfigureTestServices(IServiceCollection services)
    {
        services.AddSingleton(timeProvider);
        services.AddSingleton<ILoggerProvider>(_startupFailureLoggerProvider);
        ModuleTestSetups.ConfigureServices(services);
    }
}
