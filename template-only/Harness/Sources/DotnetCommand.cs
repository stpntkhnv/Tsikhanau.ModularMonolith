using System.Diagnostics;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Harness.Sources;

internal sealed class DotnetCommand(String workingDirectory)
{
    public Result<Unit> Run(params IReadOnlyList<String> arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet", arguments)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        process.WaitForExit();

        return process.ExitCode == 0
            ? Unit.Value
            : Error.Failure("harness.dotnet", Describe(arguments, process.ExitCode));
    }

    private String Describe(IReadOnlyList<String> arguments, Int32 exitCode) =>
        $"dotnet {String.Join(' ', arguments)} exited with {exitCode} in {workingDirectory}";
}
