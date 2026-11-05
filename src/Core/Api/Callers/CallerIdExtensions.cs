using System.Security.Claims;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Api.Callers;

internal static class CallerIdExtensions
{
    public static Optional<String> FindCallerId(this ClaimsPrincipal principal) =>
        Optional<ClaimsIdentity>
            .FromNullable(principal.Identity as ClaimsIdentity)
            .Where(identity => identity.IsAuthenticated)
            .Map(identity => identity.FindFirst(ClaimTypes.NameIdentifier))
            .Map(claim => claim.Value)
            .Where(IsNotBlank);

    private static Boolean IsNotBlank(String callerId) => !String.IsNullOrWhiteSpace(callerId);
}
