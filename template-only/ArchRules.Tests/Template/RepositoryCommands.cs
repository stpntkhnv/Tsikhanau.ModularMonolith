using System.Diagnostics;
using Tsikhanau.Monolith.Testing.Modules;

namespace Tsikhanau.Monolith.ArchRules.Tests.Template;

internal static class RepositoryCommands
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public static async Task<IReadOnlyList<String>> RunAsync(
        String fileName,
        IReadOnlyList<String> arguments,
        IReadOnlyList<String> input)
    {
        using var process = new Process { StartInfo = CreateStartInfo(fileName, arguments) };
        process.Start();
        var output = process.StandardOutput.ReadToEndAsync(CancellationToken);
        var error = process.StandardError.ReadToEndAsync(CancellationToken);
        await WriteInputAsync(process, input);
        await process.WaitForExitAsync(CancellationToken);

        process.ExitCode.ShouldBe(0, await error);

        return (await output).Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    private static ProcessStartInfo CreateStartInfo(String fileName, IReadOnlyList<String> arguments) =>
        new(fileName, arguments)
        {
            WorkingDirectory = SolutionPaths.Root,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

    private static async Task WriteInputAsync(Process process, IReadOnlyList<String> input)
    {
        var text = String.Concat(input.Select(line => $"{line}\n"));
        await using var standardInput = process.StandardInput;
        await standardInput.WriteAsync(text.AsMemory(), CancellationToken);
    }
}
