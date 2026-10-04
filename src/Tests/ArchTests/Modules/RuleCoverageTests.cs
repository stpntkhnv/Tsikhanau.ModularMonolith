using ModularMonolith.ArchTests.Support;
using ModularMonolith.BuildingBlocks.Api.Endpoints;
using ModularMonolith.BuildingBlocks.Application.Commands;
using ModularMonolith.BuildingBlocks.Application.Events;
using ModularMonolith.BuildingBlocks.Contracts.IntegrationEvents;
using ModularMonolith.BuildingBlocks.Domain.Aggregates;
using ModularMonolith.BuildingBlocks.Domain.ValueObjects;
using ModularMonolith.Shared.Reflection;
using ModularMonolith.Testing.Modules;
using ModularMonolith.TestModules.Pings.Contracts;

namespace ModularMonolith.ArchTests.Modules;

public sealed class RuleCoverageTests
{
    [Fact]
    public void Test_modules_give_the_rules_real_types_to_check()
    {
        var pingsModule = ArchModules.Get("Pings");
        var pongsModule = ArchModules.Get("Pongs");

        pingsModule
            .GetTypesIn("Domain")
            .ShouldContain(type => type.Implements<IAggregateRoot>());

        pingsModule
            .GetTypesIn("Domain")
            .ShouldContain(type => type.Implements<IValueObject>());

        pingsModule
            .GetTypesIn("Contracts")
            .ShouldContain(type => type.Implements<IntegrationEvent>());

        pingsModule
            .GetTypesIn("Features")
            .ShouldContain(type => type.Implements(typeof(ICommandHandler<,>)));

        pingsModule
            .GetTypesIn("Features")
            .Implementing<IEndpoint>()
            .ShouldNotBeEmpty();

        pongsModule
            .GetTypesIn("Features")
            .ShouldContain(type => type.Implements(typeof(IIntegrationEventHandler<>)));

        pongsModule
            .GetTypesIn("Infrastructure")
            .ShouldNotBeEmpty();

        pongsModule
            .GetTypesIn("Features")
            .ShouldContain(type => TakesDependency<IPingsQueryFacade>(type));
    }

    [Theory(SkipTestWithoutData = true)]
    [EveryProductionModule]
    public void Production_module_loads_its_project_and_its_contracts(String name) =>
        ArchModules
            .Get(name).Assemblies
            .Select(assembly => assembly.GetName().Name)
            .ShouldBe(
                [$"ModularMonolith.Modules.{name}", $"ModularMonolith.Modules.{name}.Contracts"],
                ignoreOrder: true);

    private static Boolean TakesDependency<TDependency>(Type type) =>
        type
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Any(parameter => parameter.ParameterType == typeof(TDependency));
}
