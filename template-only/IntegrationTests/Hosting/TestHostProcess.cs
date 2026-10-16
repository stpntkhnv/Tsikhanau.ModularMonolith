using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Tsikhanau.Monolith.IntegrationTests.Hosting;

internal sealed class TestHostProcess : IDisposable
{
    private const String TestHostAssembly = "Tsikhanau.Monolith.TestHost.dll";

    private const String Healthy = "Healthy";

    private const String WithoutCoreDumps = "ulimit -c 0 && exec \"$0\" \"$@\"";

    private static readonly String DotnetHost =
        Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? GetDotnetOfTheRunningRuntime();

    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan HealthTimeout = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan HealthPollInterval = TimeSpan.FromMilliseconds(200);

    private readonly Process _process;
    private readonly Task<String> _outputReading;
    private readonly Task<String> _errorReading;

    private TestHostProcess(Process process, Task<String> outputReading)
    {
        _process = process;
        _outputReading = outputReading;
        _errorReading = process.StandardError.ReadToEndAsync(CancellationToken);
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private static String TestHostPath => Path.Combine(AppContext.BaseDirectory, TestHostAssembly);

    public static TestHostProcess Start(IReadOnlyDictionary<String, String> environment) => Start(environment, []);

    public static TestHostProcess Start(IReadOnlyDictionary<String, String> environment, String[] arguments) =>
        StartReadingOutput(CreateStartInfo(arguments), environment);

    public static TestHostProcess StartInShell(
        String script,
        IReadOnlyDictionary<String, String> environment,
        String[] arguments) =>
        StartReadingOutput(CreateShellStartInfo(script, arguments), environment);

    public static TestHostProcess StartWithClosedOutput(
        IReadOnlyDictionary<String, String> environment,
        String[] arguments)
    {
        var process = StartProcess(CreateStartInfo(arguments), environment);
        process.StandardOutput.Dispose();
        return new TestHostProcess(process, Task.FromResult(String.Empty));
    }

    public async Task<TestHostExit> WaitForExitAsync()
    {
        try
        {
            await _process
                .WaitForExitAsync(CancellationToken)
                .WaitAsync(ExitTimeout, CancellationToken);
        }
        finally
        {
            _process.Kill(entireProcessTree: true);
        }

        return await ReadExitAsync();
    }

    public async Task WaitUntilHealthyAsync(String url)
    {
        using var httpClient = new HttpClient { Timeout = HealthPollInterval * 10 };
        var stopwatch = Stopwatch.StartNew();
        while (!_process.HasExited)
        {
            stopwatch.Elapsed.ShouldBeLessThan(HealthTimeout);
            if (await ReadHealthAsync(httpClient, url) == Healthy)
            {
                return;
            }

            await Task.Delay(HealthPollInterval, CancellationToken);
        }

        var exit = await ReadExitAsync();
        throw new InvalidOperationException($"The test host exited with {exit.Code} before /health:\n{exit.Text}");
    }

    public void Dispose()
    {
        _process.Kill(entireProcessTree: true);
        _process.Dispose();
    }

    private static async Task<String> ReadHealthAsync(HttpClient httpClient, String url)
    {
        try
        {
            return await httpClient.GetStringAsync($"{url}/health", CancellationToken);
        }
        catch (HttpRequestException)
        {
            return String.Empty;
        }
    }

    private async Task<TestHostExit> ReadExitAsync()
    {
        await _process.WaitForExitAsync(CancellationToken);
        var output = await _outputReading;
        var error = await _errorReading;

        return new TestHostExit(_process.ExitCode, output, error);
    }

    private static TestHostProcess StartReadingOutput(
        ProcessStartInfo startInfo,
        IReadOnlyDictionary<String, String> environment)
    {
        var process = StartProcess(startInfo, environment);
        return new TestHostProcess(process, process.StandardOutput.ReadToEndAsync(CancellationToken));
    }

    private static Process StartProcess(ProcessStartInfo startInfo, IReadOnlyDictionary<String, String> environment)
    {
        startInfo.WorkingDirectory = AppContext.BaseDirectory;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        foreach (var (name, value) in environment)
        {
            startInfo.Environment[name] = value;
        }

        var process = new Process { StartInfo = startInfo };
        process.Start();
        return process;
    }

    private static ProcessStartInfo CreateStartInfo(String[] arguments) =>
        OperatingSystem.IsWindows()
            ? new ProcessStartInfo(DotnetHost, [TestHostPath, .. arguments])
            : CreateShellStartInfo(WithoutCoreDumps, arguments);

    private static ProcessStartInfo CreateShellStartInfo(String script, String[] arguments) =>
        new("/bin/sh", ["-c", script, DotnetHost, TestHostPath, .. arguments]);

    private static String GetDotnetOfTheRunningRuntime() =>
        Path.GetFullPath(
            Path.Combine(
                RuntimeEnvironment.GetRuntimeDirectory(),
                "../../..",
                OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));
}
