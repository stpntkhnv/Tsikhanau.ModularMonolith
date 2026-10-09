using System.Reflection;
using Tsikhanau.Monolith.Api.Endpoints;
using Tsikhanau.Monolith.Application.Modules;
using Tsikhanau.Monolith.Contracts.IntegrationEvents;
using Tsikhanau.Monolith.Domain.Aggregates;
using Tsikhanau.Monolith.Infrastructure.Persistence;
using Tsikhanau.Monolith.Shared.Reflection;

namespace Tsikhanau.Monolith.ArchRules.Support;

internal static class CoreAssemblies
{
    public static readonly Assembly Shared = typeof(ReflectionExtensions).Assembly;

    public static readonly IReadOnlyList<Assembly> BuildingBlocks =
    [
        typeof(AggregateRoot<>).Assembly,
        typeof(IntegrationEvent).Assembly,
        typeof(IModuleFacade).Assembly,
        typeof(ModuleDbContext).Assembly,
        typeof(IEndpoint).Assembly,
    ];
}
