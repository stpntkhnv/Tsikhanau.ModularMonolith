using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Api.Endpoints;
using Tsikhanau.Monolith.Api.Modules;
using Tsikhanau.Monolith.TestModules.Pings.Infrastructure;
using Tsikhanau.Monolith.TestModules.Pings.Settings;

namespace Tsikhanau.Monolith.TestModules.Pings;

public sealed class PingsModule : Module
{
    public override String Name => "Pings";

    public override void MapEndpoints(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGroup("/pings")
            .WithTags(Name)
            .MapEndpointsOf<PingsModule>();

    protected override void AddServices(IServiceCollection services) =>
        AddSettings<PingsSettings>(services)
            .AddPingsInfrastructure();
}
