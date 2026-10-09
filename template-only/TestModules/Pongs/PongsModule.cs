using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Api.Endpoints;
using Tsikhanau.Monolith.Api.Modules;
using Tsikhanau.Monolith.TestModules.Pongs.Infrastructure;
using Tsikhanau.Monolith.TestModules.Pongs.Settings;

namespace Tsikhanau.Monolith.TestModules.Pongs;

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
