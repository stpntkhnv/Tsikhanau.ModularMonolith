using Microsoft.AspNetCore.Hosting;
using Tsikhanau.Monolith.Shared.Collections;
using Tsikhanau.Monolith.Testing.App;

namespace Tsikhanau.Monolith.IntegrationTests.Hosting;

internal sealed class TestAppFactory(
    TimeProvider timeProvider,
    String environmentName,
    IReadOnlyDictionary<String, String> settings)
    : AppFactory<Program>(timeProvider, environmentName)
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseContentRoot(AppContext.BaseDirectory);
        settings.ForEach(setting => builder.UseSetting(setting.Key, setting.Value));
    }
}
