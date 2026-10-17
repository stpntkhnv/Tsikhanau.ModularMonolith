using Microsoft.EntityFrameworkCore;
using Tsikhanau.Monolith.Infrastructure.Inbox;
using Tsikhanau.Monolith.Infrastructure.Queues;
using Tsikhanau.Monolith.TestModules.Pongs;
using Tsikhanau.Monolith.TestModules.Pongs.Features;
using Tsikhanau.Monolith.TestModules.Pongs.Infrastructure.Persistence;

namespace Tsikhanau.Monolith.TestHost.Probes;

internal static class ShutdownProbeExtensions
{
    private const String BlockAfterPongWriteKey = "TestHost:BlockAfterPongWrite";

    private const String BlockUntilStoppingKey = "TestHost:BlockUntilStopping";

    private const String SecretEffectPathKey = "TestHost:SecretEffectPath";

    private const String BlockSecretHandlerKey = "TestHost:BlockSecretHandler";

    private const String HoldStopUntilClassifiedKey = "TestHost:HoldStopUntilClassified";

    private const String ReportPongsPassesKey = "TestHost:ReportPongsPasses";

    private const String PongsModuleName = "Pongs";

    public static void AddShutdownProbes(this WebApplicationBuilder builder)
    {
        builder.AddPongWriteBlocker();
        builder.AddSecretHandlerProbe();
        if (builder.Configuration.GetValue<Boolean>(HoldStopUntilClassifiedKey))
        {
            builder.Services.AddHostedService(_ => new OutcomeStopBarrier(PongsModuleName, InboxMessage.Table));
        }

        if (builder.Configuration.GetValue<Boolean>(ReportPongsPassesKey))
        {
            builder.Services.AddSingleton<IQueuePump<IPongsModule>>(CreatePassReportingQueuePump);
        }
    }

    private static void AddPongWriteBlocker(this WebApplicationBuilder builder)
    {
        var blockDuration = builder.Configuration.GetValue<TimeSpan>(BlockAfterPongWriteKey);
        var blocksUntilStopping = builder.Configuration.GetValue<Boolean>(BlockUntilStoppingKey);
        if (blockDuration > TimeSpan.Zero)
        {
            builder.Services.ConfigureDbContext<PongsDbContext>(
                (serviceProvider, dbContextOptionsBuilder) => dbContextOptionsBuilder.AddInterceptors(
                    CreatePongWriteBlocker(serviceProvider, blockDuration, blocksUntilStopping)),
                ServiceLifetime.Singleton);
        }
    }

    private static void AddSecretHandlerProbe(this WebApplicationBuilder builder)
    {
        var effectPath = builder.Configuration[SecretEffectPathKey];
        if (!String.IsNullOrEmpty(effectPath))
        {
            var secretHandlerProbe = new SecretHandlerProbe(
                builder.Configuration.GetValue<TimeSpan>(BlockSecretHandlerKey),
                effectPath);

            builder.Services.AddSingleton(
                _ => new PongTestHooks { WhileHandlingSecret = secretHandlerProbe.HandleAsync });
        }
    }

    private static PongWriteBlocker CreatePongWriteBlocker(
        IServiceProvider serviceProvider,
        TimeSpan blockDuration,
        Boolean blocksUntilStopping) =>
        new(
            blockDuration,
            blocksUntilStopping
                ? serviceProvider.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping
                : CancellationToken.None);

    private static PassReportingQueuePump CreatePassReportingQueuePump(IServiceProvider serviceProvider) =>
        new(ActivatorUtilities.CreateInstance<ModuleQueuePump<IPongsModule>>(serviceProvider));
}
