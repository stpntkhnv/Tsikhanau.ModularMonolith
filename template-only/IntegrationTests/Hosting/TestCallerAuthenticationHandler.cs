using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Tsikhanau.Monolith.IntegrationTests.Hosting;

internal sealed class TestCallerAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> optionsMonitor,
    ILoggerFactory loggerFactory,
    UrlEncoder urlEncoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(optionsMonitor, loggerFactory, urlEncoder)
{
    public const String SchemeName = "TestCaller";

    public const String CallerHeader = "X-Test-Caller";

    public const String CallerWithoutIdHeader = "X-Test-Caller-Without-Id";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
        Task.FromResult(Authenticate(Request.Headers));

    private static AuthenticateResult Authenticate(IHeaderDictionary headers)
    {
        if (headers.ContainsKey(CallerWithoutIdHeader))
        {
            return Success([]);
        }

        return headers.TryGetValue(CallerHeader, out var callerId)
            ? Success([new Claim(ClaimTypes.NameIdentifier, callerId.ToString())])
            : AuthenticateResult.NoResult();
    }

    private static AuthenticateResult Success(IEnumerable<Claim> claims) =>
        AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName)), SchemeName));
}
