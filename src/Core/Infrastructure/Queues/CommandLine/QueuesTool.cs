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
                    command => command
                        .RunAsync(serviceProvider, outputTextWriter, cancellationToken)
                        .MatchAsync(_ => Task.FromResult(DoneExitCode), ReportConflictAsync),
                    ReportUsageAsync);
        }
        catch (Exception exception) when (IsDatabaseOrOutputFailure(exception))
        {
            return await ReportFailureAsync(exception);
        }
    }

    private static Boolean IsDatabaseOrOutputFailure(Exception exception) =>
        exception is NpgsqlException or IOException or InvalidOperationException { InnerException: NpgsqlException };

    private async Task<Int32> ReportUsageAsync(Error error)
    {
        await errorTextWriter.WriteLineAsync($"{Name}: {error.Message}");
        await errorTextWriter.WriteLineAsync(QueuesCommandLine.Usage);
        return UsageExitCode;
    }

    private async Task<Int32> ReportConflictAsync(Error error)
    {
        await errorTextWriter.WriteLineAsync($"{Name}: {error.Message}");
        return ConflictExitCode;
    }

    private async Task<Int32> ReportFailureAsync(Exception exception)
    {
        await errorTextWriter.WriteLineAsync($"{Name}: {exception.Message}");
        return FailedExitCode;
    }
}
