using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Tsikhanau.Monolith.Testing.App;

public abstract class AppFactory<TEntryPoint>(TimeProvider timeProvider, String environmentName)
    : WebApplicationFactory<TEntryPoint>
    where TEntryPoint : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environmentName);
        builder.ConfigureTestServices(services => services.AddSingleton(timeProvider));
    }
}
