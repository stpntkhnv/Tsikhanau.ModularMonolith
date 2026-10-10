using Tsikhanau.Monolith.Scaffold.Apps;
using Tsikhanau.Monolith.Scaffold.Files;
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
                command => RunAsync(command, cancellationToken),
                error => Task.FromResult(ReportFailure(error, UsageExitCode)));

    public async Task<Int32> RunAsync(IScaffoldCommand command, CancellationToken cancellationToken)
    {
        var outcome = await fileSystem
            .Attempt(() => fileSystem.Lock(LockPath), LockedOut)
            .BindAsync(appLock => RunLockedAsync(command, appLock, cancellationToken));

        return outcome.Match(ReportSuccess, error => ReportFailure(error, FailedExitCode));
    }

    private async Task<Result<ScaffoldPlan>> RunLockedAsync(
        IScaffoldCommand command,
        IDisposable appLock,
        CancellationToken cancellationToken)
    {
        using (appLock)
        {
            return await new AppLayoutReader(fileSystem)
                .Read()
                .Bind(app => command.Plan(app, fileSystem))
                .BindAsync(plan => RunPlanAsync(plan, cancellationToken));
        }
    }

    private Task<Result<ScaffoldPlan>> RunPlanAsync(ScaffoldPlan plan, CancellationToken cancellationToken) =>
        new Transaction(fileSystem, processRunner)
            .RunAsync(plan, cancellationToken)
            .MapAsync(_ => plan);

    private Int32 ReportSuccess(ScaffoldPlan plan)
    {
        outputTextWriter.WriteLine($"scaffold: done; {plan.NextStep}");
        plan.Note.Tap(note => outputTextWriter.WriteLine($"scaffold: note: {note}"));
        return SucceededExitCode;
    }

    private Int32 ReportFailure(Error error, Int32 exitCode)
    {
        errorTextWriter.WriteLine($"scaffold: {error.Message}");
        return exitCode;
    }
}
