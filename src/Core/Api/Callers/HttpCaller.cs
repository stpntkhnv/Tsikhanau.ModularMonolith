using Microsoft.AspNetCore.Http;
using Tsikhanau.Monolith.Application.Callers;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Api.Callers;

internal sealed class HttpCaller(IHttpContextAccessor httpContextAccessor) : ICaller
{
    public Optional<String> Id =>
        Optional<HttpContext>
            .FromNullable(httpContextAccessor.HttpContext)
            .Bind(httpContext => httpContext.User.CallerId);
}
