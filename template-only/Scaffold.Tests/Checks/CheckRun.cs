namespace Tsikhanau.Monolith.Scaffold.Tests.Checks;

internal sealed record CheckRun(Int32 ExitCode, String Output, String Error)
{
    public IReadOnlyList<String> ErrorLines => Error.Split('\n', StringSplitOptions.RemoveEmptyEntries);
}
