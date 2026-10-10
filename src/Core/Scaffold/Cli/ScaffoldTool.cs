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

    public Task<Int32> RunAsync(IReadOnlyList<String> arguments, CancellationToken cancellationToken) =>
        ScaffoldCommandLine
            .Parse(arguments)
            .Match(
                command => RunAsync(command, cancellationToken),
                error => Task.FromResult(ReportFailure(error, UsageExitCode)));

    public async Task<Int32> RunAsync(IScaffoldCommand command, CancellationToken cancellationToken)
    {
        var outcome = await new AppLayoutReader(fileSystem)
            .Read()
            .Bind(command.Plan)
            .BindAsync(plan => new Transaction(fileSystem, processRunner).RunAsync(plan, cancellationToken));

        return outcome.Match(_ => ReportSuccess(), error => ReportFailure(error, FailedExitCode));
    }

    private Int32 ReportSuccess()
    {
        outputTextWriter.WriteLine("scaffold: done; next, run ./gate.sh and commit");
        return SucceededExitCode;
    }

    private Int32 ReportFailure(Error error, Int32 exitCode)
    {
        errorTextWriter.WriteLine($"scaffold: {error.Message}");
        return exitCode;
    }
}
