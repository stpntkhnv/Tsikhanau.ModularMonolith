using Tsikhanau.Monolith.Scaffold.Apps;
using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Monolith.Scaffold.Plans;
using Tsikhanau.Monolith.Scaffold.Processes;
using Tsikhanau.Monolith.Scaffold.Transactions;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Cli;

internal sealed class ScaffoldTool(
    IFileSystem fileSystem,
    IProcessRunner processRunner,
    TextWriter outputTextWriter,
    TextWriter errorTextWriter)
{
    private const Int32 SucceededExitCode = 0;

    private const Int32 FailedExitCode = 1;

    private const Int32 UsageExitCode = 2;

    private const String LockPath = "src/Core/Scaffold/obj/scaffold.lock";

    private const String LockedOut =
        $"cannot take the scaffold lock {LockPath}, which another scaffold command in this app holds while it runs; "
        + "run this one when that one ends";

    public Task<Int32> RunAsync(IReadOnlyList<String> arguments, CancellationToken cancellationToken) =>
        ScaffoldCommandLine
            .Parse(arguments)
            .Match(
                scaffold => ScaffoldAsync(scaffold, cancellationToken),
                error => Task.FromResult(ReportUsageError(error)));

    public async Task<Int32> ScaffoldAsync(IScaffold scaffold, CancellationToken cancellationToken)
    {
        var outcome = await HoldingAppLockAsync(() => PlanAndWriteAsync(scaffold, cancellationToken));

        return outcome.Match(ReportSuccess, ReportFailure);
    }

    private Task<Result<ScaffoldPlan>> HoldingAppLockAsync(Func<Task<Result<ScaffoldPlan>>> planAndWriteAsync) =>
        fileSystem
            .Attempt(() => fileSystem.Lock(LockPath), LockedOut)
            .BindAsync(async appLock =>
            {
                using (appLock)
                {
                    return await planAndWriteAsync();
                }
            });

    private Task<Result<ScaffoldPlan>> PlanAndWriteAsync(IScaffold scaffold, CancellationToken cancellationToken) =>
        new AppLayoutReader(fileSystem)
            .Read()
            .Bind(app => scaffold.Plan(app, fileSystem))
            .BindAsync(plan => WriteAsync(plan, cancellationToken));

    private Task<Result<ScaffoldPlan>> WriteAsync(ScaffoldPlan plan, CancellationToken cancellationToken) =>
        new Transaction(fileSystem, processRunner)
            .RunAsync(plan, cancellationToken)
            .MapAsync(_ => plan);

    private Int32 ReportSuccess(ScaffoldPlan plan)
    {
        outputTextWriter.WriteLine($"scaffold: done; {plan.NextStep}");
        plan.Note.Tap(note => outputTextWriter.WriteLine($"scaffold: note: {note}"));
        return SucceededExitCode;
    }

    private Int32 ReportFailure(Error error)
    {
        errorTextWriter.WriteLine($"scaffold: {error.Message}");
        return FailedExitCode;
    }

    private Int32 ReportUsageError(Error error)
    {
        errorTextWriter.WriteLine($"scaffold: {error.Message}");
        return UsageExitCode;
    }
}
