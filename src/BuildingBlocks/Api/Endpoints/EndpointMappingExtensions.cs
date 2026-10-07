using Microsoft.AspNetCore.Routing;
using ModularMonolith.BuildingBlocks.Api.Modules;
using ModularMonolith.Shared.Collections;
using ModularMonolith.Shared.Reflection;

namespace ModularMonolith.BuildingBlocks.Api.Endpoints;

public static class EndpointMappingExtensions
{
    public static IEndpointRouteBuilder MapEndpointsOf<TModule>(this IEndpointRouteBuilder endpoints)
        where TModule : IModule
    {
        typeof(TModule)
            .CreateInstancesInItsNamespace<IEndpoint>()
            .ForEach(endpoint => endpoint.Map(endpoints));

        return endpoints;
    }
}
