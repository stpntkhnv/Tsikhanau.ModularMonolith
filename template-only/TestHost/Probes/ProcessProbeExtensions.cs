using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Tsikhanau.Monolith.TestModules.Pings.Infrastructure.Persistence;

namespace Tsikhanau.Monolith.TestHost.Probes;

internal static class ProcessProbeExtensions
{
    private const String StartRecordPathKey = "TestHost:StartRecordPath";

    private const String AddUnmigratedPropertyKey = "TestHost:AddUnmigratedProperty";

    private const String EchoRequestKey = "TestHost:EchoRequest";

    private const String KnownProxyKey = "TestHost:KnownProxy";

    private const String KnownNetworkKey = "TestHost:KnownNetwork";

    public static WebApplicationBuilder AddProcessProbes(this WebApplicationBuilder builder)
    {
        var startRecordPath = builder.Configuration[StartRecordPathKey];
        if (!String.IsNullOrEmpty(startRecordPath))
        {
            builder.Services.AddHostedService(_ => new StartRecorder(startRecordPath));
        }

        if (builder.Configuration.GetValue<Boolean>(AddUnmigratedPropertyKey))
        {
            builder.Services.ConfigureDbContext<PingsDbContext>(AddUnmigratedProperty);
        }

        builder.AddForwardedHeadersProbes();
        builder.AddShutdownProbes();

        return builder;
    }

    private static void AddForwardedHeadersProbes(this WebApplicationBuilder builder)
    {
        if (builder.Configuration.GetValue<Boolean>(EchoRequestKey))
        {
            builder.Services.AddSingleton<IStartupFilter, RequestEchoStartupFilter>();
        }

        var knownProxy = builder.Configuration[KnownProxyKey];
        var knownNetwork = builder.Configuration[KnownNetworkKey];
        if (!String.IsNullOrEmpty(knownProxy))
        {
            builder.Services.Configure<ForwardedHeadersOptions>(
                forwardedHeadersOptions => forwardedHeadersOptions.KnownProxies.Add(IPAddress.Parse(knownProxy)));
        }

        if (!String.IsNullOrEmpty(knownNetwork))
        {
            builder.Services.Configure<ForwardedHeadersOptions>(
                forwardedHeadersOptions => forwardedHeadersOptions.KnownIPNetworks.Add(IPNetwork.Parse(knownNetwork)));
        }
    }

    private static void AddUnmigratedProperty(DbContextOptionsBuilder dbContextOptionsBuilder) =>
        dbContextOptionsBuilder.ReplaceService<IModelCustomizer, UnmigratedPropertyModelCustomizer>();
}
