using Tsikhanau.Monolith.Api.Endpoints;
using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.Application.Events;
using Tsikhanau.Monolith.ArchRules.Modules;
using Tsikhanau.Monolith.ArchRules.Tests.Inputs;
using Tsikhanau.Monolith.Contracts.IntegrationEvents;
using Tsikhanau.Monolith.Domain.Aggregates;
using Tsikhanau.Monolith.Domain.ValueObjects;
using Tsikhanau.Monolith.Shared.Reflection;
using Tsikhanau.Monolith.TestModules.Pings.Contracts;

namespace Tsikhanau.Monolith.ArchRules.Tests.Modules;

public sealed class RuleCoverageTests : RuleCoverageTests<TestModuleInputs>
{
    [Fact]
    public void Test_modules_give_the_rules_real_types_to_check()
    {
        var pingsModule = GetModule("Pings");
        var pongsModule = GetModule("Pongs");

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

    private static Boolean TakesDependency<TDependency>(Type type) =>
        type
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Any(parameter => parameter.ParameterType == typeof(TDependency));
}
