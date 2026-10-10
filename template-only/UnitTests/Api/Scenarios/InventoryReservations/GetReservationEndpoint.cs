using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Tsikhanau.Monolith.Api.Endpoints;

namespace Tsikhanau.Monolith.UnitTests.Api.Scenarios.InventoryReservations;

internal sealed class GetReservationEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpoints) => endpoints.MapGet("/{reservationId:guid}", () => "reservation");
}
