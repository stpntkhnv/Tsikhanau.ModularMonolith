using System.Reflection;
using Microsoft.AspNetCore.Routing;
using ModularMonolith.Shared.Collections;
using ModularMonolith.Shared.Reflection;

namespace ModularMonolith.BuildingBlocks.Api.Endpoints;

public static class EndpointMappingExtensions
{
    public static IEndpointRouteBuilder MapEndpointsFrom(this IEndpointRouteBuilder endpoints, Assembly assembly)
    {
        assembly
            .CreateInstancesOf<IEndpoint>()
            .ForEach(endpoint => endpoint.Map(endpoints));

        return endpoints;
    }
}
