using Microsoft.AspNetCore.Authorization;

namespace Tsikhanau.Monolith.Api.Callers;

public static class CallerAuthorizationExtensions
{
    public static AuthorizationPolicyBuilder RequireCaller(
        this AuthorizationPolicyBuilder authorizationPolicyBuilder) =>
        authorizationPolicyBuilder.RequireAssertion(context => context.User.CallerId.HasValue);
}
