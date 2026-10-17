namespace Tsikhanau.Monolith.IntegrationTests.Hosting;

internal sealed record TestHostExit(Int32 Code, String Output, String Error, TimeSpan SinceSignal)
{
    public String Text => $"{Output}\n{Error}";
}
