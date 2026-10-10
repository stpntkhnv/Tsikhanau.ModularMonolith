using System.ComponentModel;
using System.Diagnostics;
using Tsikhanau.Monolith.Shared.Collections;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Transactions;

internal sealed class ProcessRunner(String workingDirectory, TextWriter textWriter) : IProcessRunner
{
    public async Task<Result<Unit>> RunAsync(ExternalStep step, CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = CreateStartInfo(step) };
        var startedAt = Stopwatch.GetTimestamp();
        await textWriter.WriteLineAsync($"scaffold: running {step}");

        var outcome = await Start(process, step)
            .BindAsync(_ => WaitForExitAsync(process, step, cancellationToken));

        var elapsedSeconds = Stopwatch.GetElapsedTime(startedAt).TotalSeconds;
        await textWriter.WriteLineAsync($"scaffold: {step} took {elapsedSeconds:F1} s");
        return outcome;
    }

    private ProcessStartInfo CreateStartInfo(ExternalStep step)
    {
        var startInfo = new ProcessStartInfo(step.FileName)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
        };

        step.Arguments.ForEach(startInfo.ArgumentList.Add);

        return startInfo;
    }

    private static Result<Unit> Start(Process process, ExternalStep step)
    {
        try
        {
            process.Start();
            return Unit.Value;
        }
        catch (Win32Exception exception)
        {
            return Error.Failure("scaffold.step", $"cannot start {step}: {exception.Message}");
        }
    }

    private static async Task<Result<Unit>> WaitForExitAsync(
        Process process,
        ExternalStep step,
        CancellationToken cancellationToken)
    {
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            return Error.Failure("scaffold.step", $"{step} was stopped");
        }

        return process.ExitCode == 0
            ? Unit.Value
            : Error.Failure("scaffold.step", $"{step} failed with exit code {process.ExitCode}");
    }
}
