using System.Diagnostics;
using Tsikhanau.Monolith.Scaffold.Transactions;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Tests.Transactions;

public sealed class ProcessRunnerTests : IDisposable
{
    private static readonly TimeSpan PatienceLimit = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(20);

    private readonly DirectoryInfo _workingDirectory = Directory.CreateTempSubdirectory("scaffold-runner-");

    private readonly StringWriter _output = new();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private ProcessRunner Runner => new(_workingDirectory.FullName, _output);

    public void Dispose()
    {
        _output.Dispose();
        _workingDirectory.Delete(recursive: true);
    }

    [Fact]
    public async Task Step_runs_in_the_working_directory_and_is_announced()
    {
        var step = new ExternalStep("bash", ["-c", "echo ran > ran.txt"]);

        var outcome = await Runner.RunAsync(step, CancellationToken);

        var hasRun = File.Exists(Path.Combine(_workingDirectory.FullName, "ran.txt"));
        var output = _output.ToString();

        outcome.ShouldBe(Result.Success());
        hasRun.ShouldBeTrue();
        output.ShouldStartWith("scaffold: running bash -c echo ran > ran.txt\n");
    }

    [Fact]
    public async Task Step_that_fails_names_its_exit_code()
    {
        var outcome = await Runner.RunAsync(new ExternalStep("bash", ["-c", "exit 3"]), CancellationToken);

        var message = ReadMessage(outcome);

        message.ShouldBe("bash -c exit 3 failed with exit code 3");
    }

    [Fact]
    public async Task Step_that_cannot_start_names_its_program()
    {
        var step = new ExternalStep("scaffold-test-no-such-program", []);

        var outcome = await Runner.RunAsync(step, CancellationToken);

        var message = ReadMessage(outcome);

        message.ShouldStartWith("cannot start scaffold-test-no-such-program: ");
    }

    [Fact]
    public async Task Stopped_step_kills_its_whole_process_tree()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
        var step = new ExternalStep("bash", ["-c", "sleep 300 & echo $! > child.pid; wait"]);

        var running = Runner.RunAsync(step, cancellation.Token);
        var childId = await ReadChildIdAsync();
        var wasChildRunning = IsRunning(childId);
        await cancellation.CancelAsync();
        var outcome = await running;
        var isChildRunning = await WaitWhileRunningAsync(childId);
        var message = ReadMessage(outcome);

        wasChildRunning.ShouldBeTrue();
        message.ShouldBe("bash -c sleep 300 & echo $! > child.pid; wait was stopped");
        isChildRunning.ShouldBeFalse();
    }

    private async Task<Int32> ReadChildIdAsync()
    {
        var path = Path.Combine(_workingDirectory.FullName, "child.pid");
        var startedAt = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(startedAt) < PatienceLimit)
        {
            var text = File.Exists(path) ? await File.ReadAllTextAsync(path, CancellationToken) : String.Empty;
            if (text.EndsWith('\n'))
            {
                return Int32.Parse(text, provider: null);
            }

            await Task.Delay(PollInterval, CancellationToken);
        }

        throw new TimeoutException($"the step wrote no {path} in {PatienceLimit}");
    }

    private static async Task<Boolean> WaitWhileRunningAsync(Int32 processId)
    {
        var startedAt = Stopwatch.GetTimestamp();
        while (IsRunning(processId) && Stopwatch.GetElapsedTime(startedAt) < PatienceLimit)
        {
            await Task.Delay(PollInterval, CancellationToken);
        }

        return IsRunning(processId);
    }

    private static Boolean IsRunning(Int32 processId)
    {
        try
        {
            var status = File.ReadAllText($"/proc/{processId}/stat");
            return status[status.LastIndexOf(')') + 2] != 'Z';
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static String ReadMessage(Result<Unit> outcome) =>
        outcome.Match(_ => "success", error => error.Message);
}
