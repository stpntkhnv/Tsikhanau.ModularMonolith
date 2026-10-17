using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.IntegrationTests.Hosting;

internal sealed class TestHostProcess : IDisposable
{
    private const String TestHostAssembly = "Tsikhanau.Monolith.TestHost.dll";

    private const String Healthy = "Healthy";

    private const String WithoutCoreDumps = "ulimit -c 0 && exec \"$0\" \"$@\"";

    private const String SendsSignal = "kill -s \"$0\" \"$1\"";

    private const String Terminate = "TERM";

    private const Int32 OutputChunkSize = 4096;

    private static readonly String DotnetHost =
        Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? GetDotnetOfTheRunningRuntime();

    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan HealthTimeout = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan HealthPollInterval = TimeSpan.FromMilliseconds(200);

    private static readonly TimeSpan OutputPollInterval = TimeSpan.FromMilliseconds(20);

    private readonly Process _process;
    private readonly Lock _outputLock = new();
    private readonly StringBuilder _output = new();
    private readonly Stopwatch _sinceSignal = new();
    private readonly Task<String> _outputReading;
    private readonly Task<String> _errorReading;

    private TestHostProcess(Process process, Boolean readsOutput)
    {
        _process = process;
        _outputReading = readsOutput ? ReadOutputAsync(process.StandardOutput) : Task.FromResult(String.Empty);
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
        return new TestHostProcess(process, readsOutput: false);
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
            _sinceSignal.Stop();
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

    public async Task<Int32> WaitForOutputAsync(String marker, TimeSpan timeout, Int32 startIndex = 0)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!_process.HasExited)
        {
            var markerEnd = FindMarkerEnd(marker, startIndex);
            if (markerEnd.HasValue)
            {
                return markerEnd.Value;
            }

            stopwatch.Elapsed.ShouldBeLessThan(timeout, $"no '{marker}' in the output:\n{ReadOutputSoFar()}");
            await Task.Delay(OutputPollInterval, CancellationToken);
        }

        var exit = await ReadExitAsync();
        var markerEndOnExit = FindMarkerEnd(marker, startIndex);

        markerEndOnExit.HasValue.ShouldBeTrue($"the test host exited with {exit.Code} before '{marker}':\n{exit.Text}");

        return markerEndOnExit.Value;
    }

    public async Task TerminateAsync()
    {
        _sinceSignal.Start();
        using var signalProcess = Process.Start(
            "/bin/sh",
            ["-c", SendsSignal, Terminate, _process.Id.ToString(CultureInfo.InvariantCulture)]);

        await signalProcess.WaitForExitAsync(CancellationToken);

        signalProcess.ExitCode.ShouldBe(0, "kill -s TERM failed");
    }

    public async Task TerminateAsync(TimeSpan gracePeriod)
    {
        await TerminateAsync();
        await Task.WhenAny(_process.WaitForExitAsync(CancellationToken), Task.Delay(gracePeriod, CancellationToken));
        if (!_process.HasExited)
        {
            _process.Kill();
        }
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

        return new TestHostExit(_process.ExitCode, output, error, _sinceSignal.Elapsed);
    }

    private async Task<String> ReadOutputAsync(StreamReader outputReader)
    {
        var chunk = new Char[OutputChunkSize];
        var readCount = await outputReader.ReadAsync(chunk, CancellationToken);
        while (readCount > 0)
        {
            AppendOutput(chunk.AsSpan(0, readCount));
            readCount = await outputReader.ReadAsync(chunk, CancellationToken);
        }

        return ReadOutputSoFar();
    }

    private void AppendOutput(ReadOnlySpan<Char> outputChunk)
    {
        lock (_outputLock)
        {
            _output.Append(outputChunk);
        }
    }

    private String ReadOutputSoFar()
    {
        lock (_outputLock)
        {
            return _output.ToString();
        }
    }

    private Optional<Int32> FindMarkerEnd(String marker, Int32 startIndex)
    {
        var output = ReadOutputSoFar();
        var markerIndex = output.IndexOf(marker, startIndex, StringComparison.Ordinal);
        return markerIndex < 0 ? Optional<Int32>.None() : markerIndex + marker.Length;
    }

    private static TestHostProcess StartReadingOutput(
        ProcessStartInfo startInfo,
        IReadOnlyDictionary<String, String> environment) =>
        new(StartProcess(startInfo, environment), readsOutput: true);

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
