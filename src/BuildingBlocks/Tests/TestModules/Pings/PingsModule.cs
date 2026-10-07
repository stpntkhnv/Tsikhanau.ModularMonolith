using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.BuildingBlocks.Api.Endpoints;
using ModularMonolith.BuildingBlocks.Api.Modules;
using ModularMonolith.TestModules.Pings.Infrastructure;
using ModularMonolith.TestModules.Pings.Settings;

namespace ModularMonolith.TestModules.Pings;

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
