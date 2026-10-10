using System.Diagnostics;
using System.Text;
using Tsikhanau.Monolith.Scaffold.Tests.Support;

namespace Tsikhanau.Monolith.Scaffold.Tests.Cli;

public sealed class StopSignalTests : IDisposable
{
    private const String RolledBack = "; files scaffold created are deleted, files it changed are back as they were\n";

    private const String ModuleProjectFolder = "src/Modules/Notes/Acme.Shop.Modules.Notes";

    private const String StepIdPath = $"{ModuleProjectFolder}/obj/step.pid";

    private const String SleepingMigration =
        $"""
        #!/usr/bin/env bash
        set -euo pipefail
        mkdir -p {ModuleProjectFolder}/Infrastructure/Migrations {ModuleProjectFolder}/obj
        echo migration > {ModuleProjectFolder}/Infrastructure/Migrations/20261010000000_Initial.cs
        echo $$ > {StepIdPath}
        sleep 300

        """;

    private static readonly TimeSpan PatienceLimit = TimeSpan.FromSeconds(30);

    private readonly SampleApp _app = SampleApp.Create();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private static String ToolAssemblyPath => Path.Combine(AppContext.BaseDirectory, "Tsikhanau.Monolith.Scaffold.dll");

    public void Dispose() => _app.Dispose();

    [Theory]
    [InlineData("INT", false)]
    [InlineData("TERM", false)]
    [InlineData("INT", true)]
    [InlineData("HUP", false)]
    [InlineData("HUP", true)]
    public async Task Signal_during_the_migration_stops_the_tool_and_puts_the_tree_back(
        String signal,
        Boolean reachesTheWholeGroup)
    {
        _app.Write("scripts/add-migration.sh", SleepingMigration);
        _app.Commit();
        var before = _app.TakeSnapshot();

        using var tool = Process.Start(CreateToolStartInfo()) ?? throw new InvalidOperationException("cannot start");
        var error = tool.StandardError.ReadToEndAsync(CancellationToken);
        var output = tool.StandardOutput.ReadToEndAsync(CancellationToken);
        var stepId = await ProcessProbe.ReadProcessIdAsync(Path.Combine(_app.Root, StepIdPath));
        var sleepId = await ProcessProbe.ReadChildIdAsync(stepId);
        await SendAsync(signal, reachesTheWholeGroup ? $"-{tool.Id}" : $"{tool.Id}");
        await tool
            .WaitForExitAsync(CancellationToken)
            .WaitAsync(PatienceLimit, CancellationToken);

        var isStepRunning = await ProcessProbe.WaitWhileRunningAsync(stepId);
        var isSleepRunning = await ProcessProbe.WaitWhileRunningAsync(sleepId);
        ProcessProbe.KillIfRunning(stepId);
        ProcessProbe.KillIfRunning(sleepId);
        var errorText = await error.WaitAsync(PatienceLimit, CancellationToken);
        var outputText = await output.WaitAsync(PatienceLimit, CancellationToken);
        var after = _app.TakeSnapshot();

        tool.ExitCode.ShouldBe(1, outputText);
        after.ShouldBe(before);
        errorText.ShouldEndWith(RolledBack, Case.Sensitive);
        isStepRunning.ShouldBeFalse();
        isSleepRunning.ShouldBeFalse();
    }

    private ProcessStartInfo CreateToolStartInfo() =>
        new("setsid", ["env", "--default-signal=INT,TERM,HUP", "dotnet", ToolAssemblyPath, "module", "Notes"])
        {
            WorkingDirectory = _app.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardErrorEncoding = Encoding.UTF8,
        };

    private static async Task SendAsync(String signal, String target)
    {
        using var kill = Process.Start("kill", ["-s", signal, "--", target])
            ?? throw new InvalidOperationException("cannot start kill");

        await kill.WaitForExitAsync(CancellationToken);

        kill.ExitCode.ShouldBe(0);
    }
}
