using System.Reflection;
using System.Runtime.CompilerServices;
using ModularMonolith.ArchTests.Support;
using Tsikhanau.Monolith.Api.Endpoints;
using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.Application.Queries;
using Tsikhanau.Monolith.Contracts.IntegrationEvents;
using Tsikhanau.Monolith.Domain.Events;
using Tsikhanau.Monolith.Shared.Reflection;

namespace ModularMonolith.ArchTests.Modules;

public sealed class NamingTests
{
    [Theory]
    [EveryModule]
    public void Types_carry_the_suffix_of_their_role(String name)
    {
        var types = ArchModules
            .Get(name).Types
            .Where(type => type.IsConcrete && !type.IsDefined(typeof(CompilerGeneratedAttribute)))
            .ToList();

        Assert.SkipWhen(types.Count == 0, $"{name} has no concrete types");

        var violations = GetViolations(types, type => type.Implements(typeof(ICommandHandler<,>)), "CommandHandler")
            .Concat(GetViolations(types, type => type.Implements(typeof(IQueryHandler<,>)), "QueryHandler"))
            .Concat(GetViolations(types, type => type.Implements<DomainEvent>(), "DomainEvent"))
            .Concat(GetViolations(types, type => type.Implements<IntegrationEvent>(), "IntegrationEvent"))
            .Concat(GetViolations(types, type => type.Implements<IEndpoint>(), "Endpoint"));

        violations.ShouldBeEmpty();
    }

    private static IEnumerable<String> GetViolations(
        IEnumerable<Type> types,
        Func<Type, Boolean> hasRole,
        String suffix) =>
        types
            .Where(hasRole)
            .Where(type => !type.Name.EndsWith(suffix, StringComparison.Ordinal))
            .Select(type => $"{type.FullName} should end with {suffix}");
}
