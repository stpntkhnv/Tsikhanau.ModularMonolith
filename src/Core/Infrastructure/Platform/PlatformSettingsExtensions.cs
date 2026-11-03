using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tsikhanau.Monolith.Infrastructure.Database.Settings;

namespace Tsikhanau.Monolith.Infrastructure.Platform;

internal static class PlatformSettingsExtensions
{
    public static DatabaseSettings ValidateSettings(this IServiceProvider serviceProvider)
    {
        serviceProvider
            .GetRequiredService<IStartupValidator>()
            .Validate();

        return serviceProvider.GetRequiredService<IOptions<DatabaseSettings>>().Value;
    }
}
