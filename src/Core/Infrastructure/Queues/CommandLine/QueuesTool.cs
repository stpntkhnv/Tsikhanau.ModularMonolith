using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Infrastructure.Queues.CommandLine;

internal sealed class QueuesTool(
    IServiceProvider serviceProvider,
    TextWriter outputTextWriter,
    TextWriter errorTextWriter)
{
    public const String Name = "queues";

    public const Int32 FailedExitCode = 1;

    private const Int32 DoneExitCode = 0;

    private const Int32 UsageExitCode = 2;

    private const Int32 ConflictExitCode = 3;

    public async Task<Int32> RunAsync(IReadOnlyList<String> arguments, CancellationToken cancellationToken)
    {
        try
        {
            return await QueuesCommandLine
                .Parse(arguments, serviceProvider.GetRequiredService<ModuleRegistry>())
                .MatchAsync(
                    command => RunAsync(command, cancellationToken),
                    error => Task.FromResult(ReportUsage(error)));
        }
        catch (Exception exception) when (IsDatabaseOrOutputFailure(exception))
        {
            return Report(exception.Message, FailedExitCode);
        }
    }

    private static Boolean IsDatabaseOrOutputFailure(Exception exception) =>
        exception is NpgsqlException or IOException or InvalidOperationException { InnerException: NpgsqlException };

    private Task<Int32> RunAsync(IQueuesCommand command, CancellationToken cancellationToken) =>
        command
            .RunAsync(serviceProvider, outputTextWriter, cancellationToken)
            .MatchAsync(_ => DoneExitCode, error => Report(error.Message, ConflictExitCode));

    private Int32 ReportUsage(Error error)
    {
        errorTextWriter.WriteLine($"{Name}: {error.Message}");
        errorTextWriter.WriteLine(QueuesCommandLine.Usage);
        return UsageExitCode;
    }

    private Int32 Report(String message, Int32 exitCode)
    {
        errorTextWriter.WriteLine($"{Name}: {message}");
        return exitCode;
    }
}
