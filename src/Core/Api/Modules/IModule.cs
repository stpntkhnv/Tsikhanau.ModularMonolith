using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Tsikhanau.Monolith.Api.Modules;

public interface IModule
{
    String Name { get; }

    void Register(IServiceCollection services, IConfigurationSection settingsSection);

    void MapEndpoints(IEndpointRouteBuilder endpoints);

    void UsePipeline(IApplicationBuilder app);
}
