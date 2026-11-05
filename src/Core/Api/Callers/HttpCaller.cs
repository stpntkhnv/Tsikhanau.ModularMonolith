using Microsoft.AspNetCore.Http;
using Tsikhanau.Monolith.Application.Callers;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Api.Callers;

internal sealed class HttpCaller(IHttpContextAccessor httpContextAccessor) : ICaller
{
    public Optional<String> Id =>
        httpContextAccessor.HttpContext
            .AsOptional()
            .Bind(httpContext => httpContext.User.FindCallerId());
}
