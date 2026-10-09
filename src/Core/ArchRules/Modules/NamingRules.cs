using System.Reflection;
using System.Runtime.CompilerServices;
using Tsikhanau.Monolith.Api.Endpoints;
using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.Application.Queries;
using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;
using Tsikhanau.Monolith.ArchRules.Support;
using Tsikhanau.Monolith.Contracts.IntegrationEvents;
using Tsikhanau.Monolith.Domain.Events;
using Tsikhanau.Monolith.Shared.Reflection;

namespace Tsikhanau.Monolith.ArchRules.Modules;

public static class NamingRules
{
    public static RuleEvaluation TypesCarryTheSuffixOfTheirRole(ArchModule module) =>
        module.Types
            .Where(type => type.IsConcrete && !type.IsDefined(typeof(CompilerGeneratedAttribute)))
            .CollectViolations(GetMissingSuffixes);

    private static IEnumerable<String> GetMissingSuffixes(Type type) =>
        GetRoles()
            .Where(role => role.HasRole(type) && !type.Name.EndsWith(role.Suffix, StringComparison.Ordinal))
            .Select(role => $"{type.FullName} should end with {role.Suffix}");

    private static IEnumerable<(Func<Type, Boolean> HasRole, String Suffix)> GetRoles() =>
    [
        (type => type.Implements(typeof(ICommandHandler<,>)), "CommandHandler"),
        (type => type.Implements(typeof(IQueryHandler<,>)), "QueryHandler"),
        (type => type.Implements<DomainEvent>(), "DomainEvent"),
        (type => type.Implements<IntegrationEvent>(), "IntegrationEvent"),
        (type => type.Implements<IEndpoint>(), "Endpoint"),
    ];
}
