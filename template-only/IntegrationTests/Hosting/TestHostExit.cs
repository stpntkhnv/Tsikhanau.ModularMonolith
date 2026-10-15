namespace Tsikhanau.Monolith.IntegrationTests.Hosting;

internal sealed record TestHostExit(Int32 Code, String Output, String Error)
{
    public String Text => $"{Output}\n{Error}";
}
