namespace Tsikhanau.Monolith.Scaffold.Tests.Samples;

internal static class FrozenPrograms
{
    public const String Core77 = """
        using Acme.Shop.Bootstrapper.Platform;
        using Tsikhanau.Monolith.Api.Modules;
        using Tsikhanau.Monolith.Hosting;
        using Tsikhanau.Monolith.Infrastructure.Diagnostics;
        using Tsikhanau.Monolith.Infrastructure.Modules;

        await using var app = BuildApp(args);
        try
        {
            await app.Services.PrepareModuleDatabasesAsync(CancellationToken.None);
            app.UseServiceDefaults();
            app.UseModules();
            app.MapModules();
            await app.StartAsync();
            await app.WaitForShutdownAsync();
        }
        catch (Exception exception)
        {
            HostLog.Terminated(app.Logger, exception);
            throw;
        }

        static WebApplication BuildApp(String[] args)
        {
            try
            {
                var builder = WebApplication.CreateBuilder(args);

                builder.AddServiceDefaults(AppDiagnostics.SourceName);
                builder.AddPlatform();
                return builder.Build();
            }
            catch (Exception exception) when (exception is not HostAbortedException)
            {
                HostLog.TerminatedBeforeTheHostWasBuilt(exception);
                throw;
            }
        }

        public partial class Program;

        """;

    public const String Core102 = """
        using Acme.Shop.Bootstrapper.Platform;
        using Tsikhanau.Monolith.Hosting;
        using Tsikhanau.Monolith.Infrastructure.Diagnostics;

        return await MonolithHost.RunAsync(args, AddServices);

        static void AddServices(WebApplicationBuilder builder)
        {
            builder.AddServiceDefaults(AppDiagnostics.SourceName);
            builder.AddPlatform();
        }

        public partial class Program;

        """;

    public const String Core103 = """
        using Tsikhanau.Monolith.Hosting;

        return await MonolithHost.RunAsync(args, AddServices);

        static void AddServices(WebApplicationBuilder builder)
        {
        }

        public partial class Program;

        """;
}
