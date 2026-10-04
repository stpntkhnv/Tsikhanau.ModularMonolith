using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ModularMonolith.ArchTests.Support;
using ModularMonolith.Shared.Reflection;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace ModularMonolith.ArchTests.Projects;

public sealed class SharedTests
{
    private static readonly Architecture Architecture =
        new ArchLoader()
            .LoadAssemblies([typeof(ReflectionExtensions).Assembly, .. ArchModules.BuildingBlockAssemblies])
            .Build();

    [Fact]
    public void Shared_code_does_not_depend_on_the_rest_of_the_app()
    {
        var sharedTypes = typeof(ReflectionExtensions).Assembly
            .GetTypes()
            .Where(type => type.Namespace == typeof(ReflectionExtensions).Namespace);

        var violations = Types()
            .That()
            .ResideInNamespaceMatching("^ModularMonolith\\.Shared(\\..+)?$")
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching("^ModularMonolith(\\.(?!Shared(\\.|$)).+)?$")
            .GetViolationsIn(Architecture);

        sharedTypes.ShouldNotBeEmpty();
        violations.ShouldBeEmpty();
    }
}
