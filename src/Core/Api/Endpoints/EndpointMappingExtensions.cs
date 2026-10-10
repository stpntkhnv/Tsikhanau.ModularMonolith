using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Tsikhanau.Monolith.Api.Modules;
using Tsikhanau.Monolith.Shared.Collections;
using Tsikhanau.Monolith.Shared.Reflection;

namespace Tsikhanau.Monolith.Api.Endpoints;

public static class EndpointMappingExtensions
{
    private static readonly Int32[] ProblemStatusCodes =
    [
        StatusCodes.Status401Unauthorized,
        StatusCodes.Status403Forbidden,
        StatusCodes.Status404NotFound,
        StatusCodes.Status409Conflict,
        StatusCodes.Status422UnprocessableEntity,
        StatusCodes.Status500InternalServerError
    ];

    public static RouteGroupBuilder MapEndpointsOf<TModule>(this RouteGroupBuilder group)
        where TModule : IModule =>
        group.MapEndpointsOf(typeof(TModule));

    internal static RouteGroupBuilder MapEndpointsOf(this RouteGroupBuilder group, Type moduleType)
    {
        DescribeErrorResponses(group);
        moduleType
            .CreateInstancesInItsNamespace<IEndpoint>()
            .ForEach(endpoint => endpoint.Map(group));

        return group;
    }

    private static void DescribeErrorResponses(RouteGroupBuilder group)
    {
        group.ProducesValidationProblem();
        ProblemStatusCodes.ForEach(statusCode => group.ProducesProblem(statusCode));
    }
}
