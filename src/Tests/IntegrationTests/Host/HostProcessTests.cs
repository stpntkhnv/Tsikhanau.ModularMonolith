using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Hosting;
using ModularMonolith.BuildingBlocks.Infrastructure.Platform;
using ModularMonolith.Testing.App;
using Npgsql;

namespace ModularMonolith.IntegrationTests.Host;

public sealed class HostProcessTests(AppFixture appFixture)
{
    private const String BootstrapperAssembly = "ModularMonolith.Bootstrapper.dll";

    private const String ConnectionStringWithoutSslMode =
        "Host=127.0.0.1;Port=1;Database=modularmonolith;Username=postgres;Password=postgres";

    private const String ConnectionStringToAClosedPort = ConnectionStringWithoutSslMode + ";SSL Mode=Disable";

    private const String CriticalLineOfTheHostLogger =
        "\"LogLevel\":\"Critical\",\"Category\":\"ModularMonolith.Bootstrapper\","
        + $"\"Message\":\"{CriticalMessage}\"";

    private const String AnyFreePort = "http://127.0.0.1:0";

    private const String CriticalMessage = "Host terminated unexpectedly";

    private const String WithoutCoreDumps = "ulimit -c 0 && exec \"$0\" \"$1\"";

    private static readonly String DotnetHost =
        Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? GetDotnetOfTheRunningRuntime();

    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(30);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Failed_start_writes_a_critical_line_and_exits_with_an_error_code()
    {
        using var bootstrapper = StartBootstrapper(Environments.Production, ConnectionStringWithoutSslMode);
        var outputReading = bootstrapper.StandardOutput.ReadToEndAsync(CancellationToken);
        var errorReading = bootstrapper.StandardError.ReadToEndAsync(CancellationToken);

        await WaitForExitAsync(bootstrapper);
        var output = await outputReading;
        var error = await errorReading;

        output.ShouldContain("crit: ");
        output.ShouldContain("must set SSL Mode");
        error.ShouldContain("Unhandled exception.");
        bootstrapper.ExitCode.ShouldNotBe(0);
    }

    [Fact]
    public async Task Failed_start_after_the_host_is_built_logs_through_the_host_logger_and_exits_with_an_error_code()
    {
        using var bootstrapper = StartBootstrapper(Environments.Production, ConnectionStringToAClosedPort);
        var outputReading = bootstrapper.StandardOutput.ReadToEndAsync(CancellationToken);
        var errorReading = bootstrapper.StandardError.ReadToEndAsync(CancellationToken);

        await WaitForExitAsync(bootstrapper);
        var output = await outputReading;
        var error = await errorReading;

        output.ShouldContain(CriticalLineOfTheHostLogger);
        output.ShouldContain("Failed to connect to 127.0.0.1:1");
        error.ShouldContain("Unhandled exception.");
        bootstrapper.ExitCode.ShouldNotBe(0);
    }

    [Fact]
    public async Task Failed_start_inside_the_running_host_logs_through_the_host_logger_and_exits_with_an_error_code()
    {
        using var takenPort = new TcpListener(IPAddress.Loopback, 0);
        takenPort.Start();
        var takenAddress = $"http://127.0.0.1:{((IPEndPoint)takenPort.LocalEndpoint).Port}";
        var connectionString = new NpgsqlConnectionStringBuilder(appFixture.ConnectionString)
        {
            SslMode = SslMode.Disable,
        }.ConnectionString;

        await using var otlpReceiver = OtlpReceiver.Start();
        using var bootstrapper = StartBootstrapper(
            Environments.Production,
            connectionString,
            takenAddress,
            otlpReceiver.Endpoint);

        var outputReading = bootstrapper.StandardOutput.ReadToEndAsync(CancellationToken);
        var errorReading = bootstrapper.StandardError.ReadToEndAsync(CancellationToken);

        await WaitForExitAsync(bootstrapper);
        var output = await outputReading;
        var error = await errorReading;

        output.ShouldContain(CriticalLineOfTheHostLogger);
        output.ShouldContain($"Failed to bind to address {takenAddress}: address already in use.");
        otlpReceiver.Bodies.ShouldContain(body => body.Contains(CriticalMessage, StringComparison.Ordinal));
        error.ShouldContain("Unhandled exception.");
        bootstrapper.ExitCode.ShouldNotBe(0);
    }

    private static Process StartBootstrapper(
        String environmentName,
        String connectionString,
        String urls = AnyFreePort,
        String otlpEndpoint = "")
    {
        var startInfo = CreateStartInfo(Path.Combine(AppContext.BaseDirectory, BootstrapperAssembly));
        startInfo.WorkingDirectory = AppContext.BaseDirectory;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = environmentName;
        startInfo.Environment["DOTNET_ENVIRONMENT"] = environmentName;
        startInfo.Environment["Logging__Console__FormatterName"] = "json";
        startInfo.Environment["ASPNETCORE_URLS"] = urls;
        startInfo.Environment["OTEL_EXPORTER_OTLP_ENDPOINT"] = otlpEndpoint;
        startInfo.Environment["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf";
        startInfo.Environment[$"ConnectionStrings__{PlatformServiceCollectionExtensions.ConnectionName}"] =
            connectionString;

        var bootstrapper = new Process { StartInfo = startInfo };
        bootstrapper.Start();
        return bootstrapper;
    }

    private static ProcessStartInfo CreateStartInfo(String bootstrapperPath) =>
        OperatingSystem.IsWindows()
            ? new ProcessStartInfo(DotnetHost, [bootstrapperPath])
            : new ProcessStartInfo("/bin/sh", ["-c", WithoutCoreDumps, DotnetHost, bootstrapperPath]);

    private static String GetDotnetOfTheRunningRuntime() =>
        Path.GetFullPath(
            Path.Combine(
                RuntimeEnvironment.GetRuntimeDirectory(),
                "../../..",
                OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));

    private static async Task WaitForExitAsync(Process bootstrapper)
    {
        try
        {
            await bootstrapper
                .WaitForExitAsync(CancellationToken)
                .WaitAsync(ExitTimeout, CancellationToken);
        }
        finally
        {
            bootstrapper.Kill(entireProcessTree: true);
        }
    }

    private sealed class OtlpReceiver : IAsyncDisposable
    {
        private readonly HttpListener _httpListener = new();
        private readonly ConcurrentQueue<String> _bodies = new();
        private readonly Task _receiving;

        private OtlpReceiver(Int32 port)
        {
            Endpoint = $"http://127.0.0.1:{port}";
            _httpListener.Prefixes.Add($"{Endpoint}/");
            _httpListener.Start();
            _receiving = ReceiveAsync();
        }

        public String Endpoint { get; }

        public static OtlpReceiver Start() => new(GetFreePort());

        public IReadOnlyCollection<String> Bodies => _bodies;

        public async ValueTask DisposeAsync()
        {
            _httpListener.Close();
            await _receiving;
        }

        private static Int32 GetFreePort()
        {
            using var tcpListener = new TcpListener(IPAddress.Loopback, 0);
            tcpListener.Start();
            return ((IPEndPoint)tcpListener.LocalEndpoint).Port;
        }

        private static Boolean EndsReceiving(Exception exception) =>
            exception is HttpListenerException or ObjectDisposedException or IOException;

        private async Task ReceiveAsync()
        {
            try
            {
                while (true)
                {
                    var context = await _httpListener.GetContextAsync();
                    await ReceiveAsync(context);
                }
            }
            catch (Exception exception) when (EndsReceiving(exception))
            {
            }
        }

        private async Task ReceiveAsync(HttpListenerContext context)
        {
            using var bodyReader = new StreamReader(context.Request.InputStream, Encoding.Latin1);
            _bodies.Enqueue(await bodyReader.ReadToEndAsync(TestContext.Current.CancellationToken));
            context.Response.StatusCode = (Int32)HttpStatusCode.OK;
            context.Response.Close();
        }
    }
}
