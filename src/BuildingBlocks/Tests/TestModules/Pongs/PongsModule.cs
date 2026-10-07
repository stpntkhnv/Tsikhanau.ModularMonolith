using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.BuildingBlocks.Api.Endpoints;
using ModularMonolith.BuildingBlocks.Api.Modules;
using ModularMonolith.TestModules.Pongs.Infrastructure;
using ModularMonolith.TestModules.Pongs.Settings;

namespace ModularMonolith.TestModules.Pongs;

public sealed class PongsModule : Module
{
    public override String Name => "Pongs";

    public override void MapEndpoints(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGroup("/pongs")
            .WithTags(Name)
            .MapEndpointsOf<PongsModule>();

    protected override void AddServices(IServiceCollection services) =>
        AddSettings<PongsSettings>(services)
            .AddPongsInfrastructure();
}
