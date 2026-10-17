namespace Tsikhanau.Monolith.TestHost.Probes;

internal sealed class RequestEchoStartupFilter : IStartupFilter
{
    public const String Path = "/probe/request";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        applicationBuilder =>
        {
            applicationBuilder.Map(Path, EchoRequest);
            next(applicationBuilder);
        };

    private static void EchoRequest(IApplicationBuilder applicationBuilder) =>
        applicationBuilder.Run(
            httpContext => httpContext.Response.WriteAsync(
                $"scheme={httpContext.Request.Scheme} remote={httpContext.Connection.RemoteIpAddress}",
                httpContext.RequestAborted));
}
