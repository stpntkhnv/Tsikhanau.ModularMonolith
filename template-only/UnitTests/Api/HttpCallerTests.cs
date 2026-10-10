using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Api.Callers;
using Tsikhanau.Monolith.Application.Callers;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.UnitTests.Api;

public sealed class HttpCallerTests
{
    private const String AuthenticationType = "Test";

    private const Int32 Reads = 10_000;

    private static readonly AuthorizationPolicy CallerPolicy = new AuthorizationPolicyBuilder()
        .RequireCaller()
        .Build();

    private static IEnumerable<(String Description, ClaimsPrincipal Principal)> Principals =>
    [
        ("anonymous", new ClaimsPrincipal(new ClaimsIdentity())),
        ("name identifier", Principal(Authenticated(NameIdentifier("alice")))),
        ("only sub", Principal(Authenticated(new Claim("sub", "alice")))),
        ("no id claim", Principal(Authenticated(new Claim(ClaimTypes.Name, "Alice")))),
        ("blank name identifier", Principal(Authenticated(NameIdentifier(" ")))),
        ("unauthenticated with a name identifier", Principal(Unauthenticated(NameIdentifier("alice")))),
        (
            "unauthenticated primary, authenticated second with a name identifier",
            Principal(Unauthenticated(new Claim(ClaimTypes.Name, "Nobody")), Authenticated(NameIdentifier("alice")))),
        (
            "authenticated primary without an id, unauthenticated second with a name identifier",
            Principal(Authenticated(new Claim(ClaimTypes.Name, "Bob")), Unauthenticated(NameIdentifier("bob"))))
    ];

    [Fact]
    public void Caller_has_no_id_without_a_request()
    {
        using var serviceProvider = new ServiceCollection()
            .AddCaller()
            .BuildServiceProvider();

        var caller = serviceProvider.GetRequiredService<ICaller>();

        caller.Id.ShouldBe(Optional<String>.None());
    }

    [Fact]
    public async Task Caller_and_policy_take_only_the_name_identifier_of_an_authenticated_primary_identity()
    {
        using var serviceProvider = new ServiceCollection()
            .AddCaller()
            .AddLogging()
            .AddAuthorizationCore()
            .BuildServiceProvider();

        var readings = new List<String>();
        foreach (var (description, principal) in Principals)
        {
            readings.Add(await ReadAsync(serviceProvider, description, principal));
        }

        readings.ShouldBe(
        [
            "anonymous: None, policy refuses",
            "name identifier: Some(alice), policy allows",
            "only sub: None, policy refuses",
            "no id claim: None, policy refuses",
            "blank name identifier: None, policy refuses",
            "unauthenticated with a name identifier: None, policy refuses",
            "unauthenticated primary, authenticated second with a name identifier: None, policy refuses",
            "authenticated primary without an id, unauthenticated second with a name identifier: None, policy refuses"
        ]);
    }

    [Fact]
    public void Reading_the_caller_without_a_request_allocates_nothing()
    {
        using var serviceProvider = new ServiceCollection()
            .AddCaller()
            .BuildServiceProvider();

        var caller = serviceProvider.GetRequiredService<ICaller>();
        _ = caller.Id;
        var allocatedBytesBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var read = 0; read < Reads; read++)
        {
            _ = caller.Id;
        }

        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBytesBefore;

        allocatedBytes.ShouldBe(0);
    }

    [Fact]
    public void Adding_the_caller_twice_registers_it_once()
    {
        var services = new ServiceCollection()
            .AddCaller()
            .AddCaller();

        var serviceTypes = services.Select(descriptor => descriptor.ServiceType);

        serviceTypes.ShouldBe([typeof(IHttpContextAccessor), typeof(ICaller)]);
    }

    private static async Task<String> ReadAsync(
        IServiceProvider serviceProvider,
        String description,
        ClaimsPrincipal principal)
    {
        serviceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext =
            new DefaultHttpContext { User = principal };

        var callerId = serviceProvider.GetRequiredService<ICaller>().Id;
        var authorizationResult = await serviceProvider
            .GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(principal, CallerPolicy);

        return $"{description}: {callerId}, policy {(authorizationResult.Succeeded ? "allows" : "refuses")}";
    }

    private static ClaimsPrincipal Principal(params ClaimsIdentity[] identities) => new(identities);

    private static ClaimsIdentity Authenticated(Claim claim) => new([claim], AuthenticationType);

    private static ClaimsIdentity Unauthenticated(Claim claim) => new([claim]);

    private static Claim NameIdentifier(String value) => new(ClaimTypes.NameIdentifier, value);
}
