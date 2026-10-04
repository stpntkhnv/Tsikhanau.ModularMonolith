using ModularMonolith.BuildingBlocks.Infrastructure.Platform;

namespace ModularMonolith.Bootstrapper.Platform;

internal static class PlatformExtensions
{
    public static WebApplicationBuilder AddPlatform(this WebApplicationBuilder builder)
    {
        builder.EnsureConnectionString();
        builder.Services.AddPlatformServices(builder.Configuration);
        return builder;
    }
}
