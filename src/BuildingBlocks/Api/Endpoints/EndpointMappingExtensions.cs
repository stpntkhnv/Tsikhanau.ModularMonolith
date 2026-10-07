using System.Reflection;
using Microsoft.AspNetCore.Routing;
using ModularMonolith.BuildingBlocks.Api.Modules;
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

    public static IEndpointRouteBuilder MapEndpointsOf<TModule>(this IEndpointRouteBuilder endpoints)
        where TModule : IModule
    {
        var moduleNamespace = typeof(TModule).Namespace ?? String.Empty;

        typeof(TModule).Assembly
            .GetTypes()
            .Implementing<IEndpoint>()
            .Where(type => type.IsInNamespace(moduleNamespace))
            .Select(type => type.CreateInstance<IEndpoint>())
            .ForEach(endpoint => endpoint.Map(endpoints));

        return endpoints;
    }
}
