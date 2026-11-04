using Microsoft.AspNetCore.Hosting;
using Tsikhanau.Monolith.Testing.App;

namespace Tsikhanau.Monolith.IntegrationTests.Hosts.TestApp;

public sealed class TestAppFactory(TimeProvider timeProvider, String environmentName)
    : AppFactory<Program>(timeProvider, environmentName)
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseContentRoot(AppContext.BaseDirectory);
    }
}
