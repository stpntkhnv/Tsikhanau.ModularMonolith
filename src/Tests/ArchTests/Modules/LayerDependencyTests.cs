using System.Text.RegularExpressions;
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ModularMonolith.ArchTests.Support;
using ModularMonolith.BuildingBlocks.Api.Endpoints;
using ModularMonolith.Shared.Reflection;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace ModularMonolith.ArchTests.Modules;

public sealed class LayerDependencyTests
{
    private const String ConfigurationNamespace = "^Microsoft\\.Extensions\\.Configuration(\\..+)?$";
    private const String DatabaseConnectionTypes = "^Npgsql\\.(NpgsqlConnection|NpgsqlDataSource)$";
    private const String MigrationsNamespace = "(^|\\.)Migrations(\\..+)?$";

    private static readonly String WebAndDataFrameworks = BuildNamespacePattern(
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore",
        "Npgsql",
        "NpgsqlTypes",
        "ModularMonolith.BuildingBlocks.Infrastructure",
        "ModularMonolith.BuildingBlocks.Api");

    private static readonly String PersistenceTypesOtherThanUnitOfWorkErrors = BuildTypePattern(
        [
            "Microsoft.EntityFrameworkCore",
            "Npgsql",
            "NpgsqlTypes",
            "Dapper",
            "ModularMonolith.BuildingBlocks.Infrastructure",
        ],
        allowedTypeNames: ["ModularMonolith.BuildingBlocks.Infrastructure.Persistence.UnitOfWorkErrors"]);

    private static readonly Architecture Architecture =
        new ArchLoader()
            .LoadAssemblies([.. ArchModules.ModuleAssemblies, .. ArchModules.BuildingBlockAssemblies])
            .Build();

    [Theory]
    [EveryModule]
    public void Domain_depends_only_on_itself_the_domain_building_blocks_and_railway(String name)
    {
        var module = ArchModules.Get(name);
        var allowedNamespaces = BuildNamespacePattern(
            "System",
            $"{module.RootNamespace}.Domain",
            "ModularMonolith.BuildingBlocks.Domain",
            "Tsikhanau.Railway");

        var rule = Types()
            .That()
            .ResideInNamespaceMatching(module.BuildLayerPattern("Domain"))
            .Should()
            .OnlyDependOnTypesThat()
            .ResideInNamespaceMatching(allowedNamespaces);

        Assert.SkipUnless(module.HasTypesIn("Domain"), $"{name} has no types in Domain");

        var violations = rule.GetViolationsIn(Architecture);

        violations.ShouldBeEmpty();
    }

    [Theory]
    [EveryModule]
    public void Features_do_not_depend_on_infrastructure(String name)
    {
        var module = ArchModules.Get(name);
        var rule = Types()
            .That()
            .ResideInNamespaceMatching(module.BuildLayerPattern("Features"))
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(module.BuildLayerPattern("Infrastructure"));

        Assert.SkipUnless(module.HasTypesIn("Features"), $"{name} has no types in Features");

        var violations = rule.GetViolationsIn(Architecture);

        violations.ShouldBeEmpty();
    }

    [Theory]
    [EveryModule]
    public void Features_other_than_endpoints_stay_free_of_web_and_data_frameworks(String name)
    {
        var module = ArchModules.Get(name);
        var rule = Types()
            .That()
            .ResideInNamespaceMatching(module.BuildLayerPattern("Features"))
            .And()
            .DoNotImplementInterface(typeof(IEndpoint))
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(WebAndDataFrameworks);

        Assert.SkipUnless(module.HasTypesIn("Features"), $"{name} has no types in Features");

        var violations = rule.GetViolationsIn(Architecture);

        violations.ShouldBeEmpty();
    }

    [Theory]
    [EveryModule]
    public void Endpoints_go_through_the_facade(String name)
    {
        var module = ArchModules.Get(name);
        var hasEndpoints = module.Types
            .Implementing<IEndpoint>()
            .Any();

        var rule = Classes()
            .That()
            .ImplementInterface(typeof(IEndpoint))
            .And()
            .ResideInNamespaceMatching(module.RootPattern)
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(module.BuildLayerPattern("Domain", "Infrastructure"))
            .AndShould()
            .NotDependOnAny(module.Handlers)
            .AndShould()
            .NotDependOnAny(ArchModule.HandlerInterfaces)
            .AndShould()
            .NotDependOnAnyTypesThat()
            .HaveFullNameMatching(PersistenceTypesOtherThanUnitOfWorkErrors);

        Assert.SkipUnless(hasEndpoints, $"{name} has no endpoints");

        var violations = rule.GetViolationsIn(Architecture);

        violations.ShouldBeEmpty();
    }

    [Theory]
    [EveryModule]
    public void Settings_depend_on_nothing_else_in_the_module(String name)
    {
        var module = ArchModules.Get(name);
        var rule = Types()
            .That()
            .ResideInNamespaceMatching(module.BuildLayerPattern("Settings"))
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(module.BuildLayerPattern("Features", "Infrastructure", "Http"));

        Assert.SkipUnless(module.HasTypesIn("Settings"), $"{name} has no types in Settings");

        var violations = rule.GetViolationsIn(Architecture);

        violations.ShouldBeEmpty();
    }

    [Theory]
    [EveryModule]
    public void Infrastructure_does_not_depend_on_endpoints_or_http(String name)
    {
        var module = ArchModules.Get(name);
        var rule = Types()
            .That()
            .ResideInNamespaceMatching(module.BuildLayerPattern("Infrastructure"))
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(module.BuildLayerPattern("Http"))
            .AndShould()
            .NotDependOnAnyTypesThat()
            .ImplementInterface(typeof(IEndpoint));

        Assert.SkipUnless(module.HasTypesIn("Infrastructure"), $"{name} has no types in Infrastructure");

        var violations = rule.GetViolationsIn(Architecture);

        violations.ShouldBeEmpty();
    }

    [Theory]
    [EveryModule]
    public void Contracts_depend_only_on_the_contract_building_blocks_and_railway(String name)
    {
        var module = ArchModules.Get(name);
        var allowedNamespaces = BuildNamespacePattern(
            "System",
            $"{module.RootNamespace}.Contracts",
            "ModularMonolith.BuildingBlocks.Contracts",
            "Tsikhanau.Railway");

        var rule = Types()
            .That()
            .ResideInNamespaceMatching(module.BuildLayerPattern("Contracts"))
            .Should()
            .OnlyDependOnTypesThat()
            .ResideInNamespaceMatching(allowedNamespaces);

        Assert.SkipUnless(module.HasTypesIn("Contracts"), $"{name} has no types in Contracts");

        var violations = rule.GetViolationsIn(Architecture);

        violations.ShouldBeEmpty();
    }

    [Theory]
    [EveryModule]
    public void Module_reaches_other_modules_only_through_their_contracts(String name)
    {
        var module = ArchModules.Get(name);
        var otherModules = ArchModules.Names
            .Where(otherName => otherName != name)
            .Select(ArchModules.Get);

        Assert.SkipUnless(module.HasTypes, $"{name} has no types");

        var violations = otherModules.SelectMany(otherModule => GetViolationsReaching(module, otherModule));

        violations.ShouldBeEmpty();
    }

    [Theory]
    [EveryModule]
    public void Module_does_not_read_configuration_directly(String name)
    {
        var module = ArchModules.Get(name);
        var rule = Types()
            .That()
            .ResideInNamespaceMatching(module.RootPattern)
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(ConfigurationNamespace);

        Assert.SkipUnless(module.HasTypes, $"{name} has no types");

        var violations = rule.GetViolationsIn(Architecture);

        violations.ShouldBeEmpty();
    }

    [Theory]
    [EveryModule]
    public void Module_does_not_open_its_own_database_connections(String name)
    {
        var module = ArchModules.Get(name);
        var rule = Types()
            .That()
            .ResideInNamespaceMatching(module.RootPattern)
            .And()
            .DoNotResideInNamespaceMatching(MigrationsNamespace)
            .Should()
            .NotDependOnAnyTypesThat()
            .HaveFullNameMatching(DatabaseConnectionTypes);

        Assert.SkipUnless(module.HasTypes, $"{name} has no types");

        var violations = rule.GetViolationsIn(Architecture);

        violations.ShouldBeEmpty();
    }

    private static IReadOnlyList<String> GetViolationsReaching(ArchModule module, ArchModule otherModule) =>
        Types()
            .That()
            .ResideInNamespaceMatching(module.RootPattern)
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(otherModule.OutsideContractsPattern)
            .GetViolationsIn(Architecture);

    private static String BuildNamespacePattern(params IEnumerable<String> namespaces) =>
        $"^({String.Join('|', namespaces.Select(Regex.Escape))})(\\..+)?$";

    private static String BuildTypePattern(IEnumerable<String> namespaces, IEnumerable<String> allowedTypeNames)
    {
        var allowedTypes = String.Join('|', allowedTypeNames.Select(Regex.Escape));
        var namespacePrefixes = String.Join('|', namespaces.Select(Regex.Escape));

        return $"^(?!({allowedTypes})$)({namespacePrefixes})\\..+$";
    }
}
