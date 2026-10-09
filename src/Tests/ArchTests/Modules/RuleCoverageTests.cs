using ModularMonolith.ArchTests.Support;
using Tsikhanau.Monolith.Api.Endpoints;
using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.Application.Events;
using Tsikhanau.Monolith.Contracts.IntegrationEvents;
using Tsikhanau.Monolith.Domain.Aggregates;
using Tsikhanau.Monolith.Domain.ValueObjects;
using Tsikhanau.Monolith.Shared.Reflection;
using Tsikhanau.Monolith.Testing.Modules;
using Tsikhanau.Monolith.TestModules.Pings.Contracts;

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

    [Theory]
    [EveryProductionModule]
    public void Production_module_loads_its_project_and_its_contracts(String name)
    {
        Assert.SkipWhen(name == ProductionModules.Placeholder, "There are no production modules");

        ArchModules
            .Get(name).Assemblies
            .Select(assembly => assembly.GetName().Name)
            .ShouldBe(
                [$"ModularMonolith.Modules.{name}", $"ModularMonolith.Modules.{name}.Contracts"],
                ignoreOrder: true);
    }

    private static Boolean TakesDependency<TDependency>(Type type) =>
        type
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Any(parameter => parameter.ParameterType == typeof(TDependency));
}
