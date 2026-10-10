using Tsikhanau.Monolith.Scaffold.Tests.Support;
using Tsikhanau.Monolith.Scaffold.Transactions;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Tests.Transactions;

public sealed class ProcessRunnerTests : IDisposable
{
    private static readonly TimeSpan PatienceLimit = TimeSpan.FromSeconds(10);

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
        var childId = await ProcessProbe.ReadProcessIdAsync(Path.Combine(_workingDirectory.FullName, "child.pid"));
        var wasChildRunning = ProcessProbe.IsRunning(childId);
        await cancellation.CancelAsync();
        var outcome = await running;
        var isChildRunning = await ProcessProbe.WaitWhileRunningAsync(childId);
        var message = ReadMessage(outcome);

        wasChildRunning.ShouldBeTrue();
        message.ShouldBe("bash -c sleep 300 & echo $! > child.pid; wait was stopped");
        isChildRunning.ShouldBeFalse();
    }

    [Fact]
    public async Task Step_that_ends_with_a_failure_when_the_run_is_stopped_is_reported_as_stopped()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
        var heldContinuations = new HeldContinuations();
        var step = new ExternalStep("bash", ["-c", "sleep 0.2; exit 130"]);

        var running = RunHoldingContinuations(step, heldContinuations, cancellation.Token);
        await heldContinuations.FirstHeld.WaitAsync(PatienceLimit, CancellationToken);
        await cancellation.CancelAsync();
        heldContinuations.Release();
        var outcome = await running.WaitAsync(PatienceLimit, CancellationToken);

        var message = ReadMessage(outcome);

        message.ShouldBe("bash -c sleep 0.2; exit 130 was stopped");
    }

    private Task<Result<Unit>> RunHoldingContinuations(
        ExternalStep step,
        HeldContinuations heldContinuations,
        CancellationToken cancellationToken)
    {
        var synchronizationContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(heldContinuations);
        try
        {
            return Runner.RunAsync(step, cancellationToken);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(synchronizationContext);
        }
    }

    private static String ReadMessage(Result<Unit> outcome) =>
        outcome.Match(_ => "success", error => error.Message);

    private sealed class HeldContinuations : SynchronizationContext
    {
        private readonly Lock _lock = new();

        private readonly List<(SendOrPostCallback Callback, Object? State)> _continuations = [];

        private readonly TaskCompletionSource _firstHeld = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private Boolean _isReleased;

        public Task FirstHeld => _firstHeld.Task;

        public override void Post(SendOrPostCallback d, Object? state)
        {
            lock (_lock)
            {
                if (!_isReleased)
                {
                    _continuations.Add((d, state));
                    _firstHeld.TrySetResult();
                    return;
                }
            }

            ThreadPool.QueueUserWorkItem(_ => d(state));
        }

        public void Release()
        {
            lock (_lock)
            {
                _isReleased = true;
            }

            _continuations.ForEach(continuation =>
                ThreadPool.QueueUserWorkItem(_ => continuation.Callback(continuation.State)));
        }
    }
}
