using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Tsikhanau.Monolith.TestModules.Pings.Infrastructure.Persistence;

namespace Tsikhanau.Monolith.TestHost.Probes;

internal static class ProcessProbeExtensions
{
    private const String StartRecordPathKey = "TestHost:StartRecordPath";

    private const String AddUnmigratedPropertyKey = "TestHost:AddUnmigratedProperty";

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

        return builder;
    }

    private static void AddUnmigratedProperty(DbContextOptionsBuilder dbContextOptionsBuilder) =>
        dbContextOptionsBuilder.ReplaceService<IModelCustomizer, UnmigratedPropertyModelCustomizer>();
}
