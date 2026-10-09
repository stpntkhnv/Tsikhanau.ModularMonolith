using Microsoft.AspNetCore.Routing;
using Tsikhanau.Monolith.Api.Modules;
using Tsikhanau.Monolith.Shared.Collections;
using Tsikhanau.Monolith.Shared.Reflection;

namespace Tsikhanau.Monolith.Api.Endpoints;

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
