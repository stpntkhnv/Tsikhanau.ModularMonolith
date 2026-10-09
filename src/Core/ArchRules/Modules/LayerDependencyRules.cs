using System.Text.RegularExpressions;
using ArchUnitNET.Fluent;
using Tsikhanau.Monolith.Api.Endpoints;
using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;
using Tsikhanau.Monolith.ArchRules.Support;
using Tsikhanau.Monolith.Shared.Reflection;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Tsikhanau.Monolith.ArchRules.Modules;

public static class LayerDependencyRules
{
    private const String ConfigurationNamespace = "^Microsoft\\.Extensions\\.Configuration(\\..+)?$";
    private const String DatabaseConnectionTypes = "^Npgsql\\.(NpgsqlConnection|NpgsqlDataSource)$";
    private const String MigrationsNamespace = "(^|\\.)Migrations(\\..+)?$";
    private const String FrameworksOutsideSystem = "^(Microsoft|Npgsql|NpgsqlTypes|Dapper)(\\..+)?$";

    private static readonly String WebAndDataFrameworks = BuildNamespacePattern(
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore",
        "Npgsql",
        "NpgsqlTypes",
        "Tsikhanau.Monolith.Infrastructure",
        "Tsikhanau.Monolith.Api");

    private static readonly String PersistenceTypesOtherThanUnitOfWorkErrors = BuildTypePattern(
        [
            "Microsoft.EntityFrameworkCore",
            "Npgsql",
            "NpgsqlTypes",
            "Dapper",
            "Tsikhanau.Monolith.Infrastructure",
        ],
        allowedTypeNames: ["Tsikhanau.Monolith.Infrastructure.Persistence.UnitOfWorkErrors"]);

    public static RuleEvaluation DomainDependsOnlyOnItselfTheDomainBuildingBlocksAndRailway(
        ArchModules modules,
        ArchModule module)
    {
        var allowedNamespaces = BuildNamespacePattern(
            "System",
            $"{module.RootNamespace}.Domain",
            "Tsikhanau.Monolith.Domain",
            "Tsikhanau.Railway");

        var rule = Types()
            .That()
            .ResideInNamespaceMatching(module.BuildLayerPattern("Domain"))
            .Should()
            .OnlyDependOnTypesThat()
            .ResideInNamespaceMatching(allowedNamespaces);

        return Evaluate(modules, module.GetTypesIn("Domain"), rule);
    }

    public static RuleEvaluation DomainDoesNotDependOnFrameworksOutsideSystem(ArchModules modules, ArchModule module)
    {
        var rule = Types()
            .That()
            .ResideInNamespaceMatching(module.BuildLayerPattern("Domain"))
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(FrameworksOutsideSystem);

        return Evaluate(modules, module.GetTypesIn("Domain"), rule);
    }

    public static RuleEvaluation FeaturesDoNotDependOnInfrastructure(ArchModules modules, ArchModule module)
    {
        var rule = Types()
            .That()
            .ResideInNamespaceMatching(module.BuildLayerPattern("Features"))
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(module.BuildLayerPattern("Infrastructure"));

        return Evaluate(modules, module.GetTypesIn("Features"), rule);
    }

    public static RuleEvaluation FeaturesOtherThanEndpointsStayFreeOfWebAndDataFrameworks(
        ArchModules modules,
        ArchModule module)
    {
        var rule = Types()
            .That()
            .ResideInNamespaceMatching(module.BuildLayerPattern("Features"))
            .And()
            .DoNotImplementInterface(typeof(IEndpoint))
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(WebAndDataFrameworks);

        return Evaluate(modules, module.GetTypesIn("Features"), rule);
    }

    public static RuleEvaluation EndpointsGoThroughTheFacade(ArchModules modules, ArchModule module)
    {
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

        return Evaluate(modules, module.Types.Implementing<IEndpoint>(), rule);
    }

    public static RuleEvaluation SettingsDependOnNothingElseInTheModule(ArchModules modules, ArchModule module)
    {
        var rule = Types()
            .That()
            .ResideInNamespaceMatching(module.BuildLayerPattern("Settings"))
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(module.BuildLayerPattern("Features", "Infrastructure", "Http"));

        return Evaluate(modules, module.GetTypesIn("Settings"), rule);
    }

    public static RuleEvaluation InfrastructureDoesNotDependOnEndpointsOrHttp(ArchModules modules, ArchModule module)
    {
        var rule = Types()
            .That()
            .ResideInNamespaceMatching(module.BuildLayerPattern("Infrastructure"))
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(module.BuildLayerPattern("Http"))
            .AndShould()
            .NotDependOnAnyTypesThat()
            .ImplementInterface(typeof(IEndpoint));

        return Evaluate(modules, module.GetTypesIn("Infrastructure"), rule);
    }

    public static RuleEvaluation ContractsDependOnlyOnTheContractBuildingBlocksAndRailway(
        ArchModules modules,
        ArchModule module)
    {
        var allowedNamespaces = BuildNamespacePattern(
            "System",
            $"{module.RootNamespace}.Contracts",
            "Tsikhanau.Monolith.Contracts",
            "Tsikhanau.Railway");

        var rule = Types()
            .That()
            .ResideInNamespaceMatching(module.BuildLayerPattern("Contracts"))
            .Should()
            .OnlyDependOnTypesThat()
            .ResideInNamespaceMatching(allowedNamespaces);

        return Evaluate(modules, module.GetTypesIn("Contracts"), rule);
    }

    public static RuleEvaluation ModuleReachesOtherModulesOnlyThroughTheirContracts(
        ArchModules modules,
        ArchModule module) =>
        RuleEvaluation.Create(
            module.Types,
            () => modules
                .GetOthers(module)
                .SelectMany(otherModule => GetViolationsReaching(modules, module, otherModule)));

    public static RuleEvaluation ModuleDoesNotReadConfigurationDirectly(ArchModules modules, ArchModule module)
    {
        var rule = Types()
            .That()
            .ResideInNamespaceMatching(module.RootPattern)
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(ConfigurationNamespace);

        return Evaluate(modules, module.Types, rule);
    }

    public static RuleEvaluation ModuleDoesNotOpenItsOwnDatabaseConnections(ArchModules modules, ArchModule module)
    {
        var rule = Types()
            .That()
            .ResideInNamespaceMatching(module.RootPattern)
            .And()
            .DoNotResideInNamespaceMatching(MigrationsNamespace)
            .Should()
            .NotDependOnAnyTypesThat()
            .HaveFullNameMatching(DatabaseConnectionTypes);

        return Evaluate(modules, module.Types, rule);
    }

    private static RuleEvaluation Evaluate(ArchModules modules, IEnumerable<Type> subjectTypes, IArchRule rule)
    {
        IReadOnlyList<Type> types = [.. subjectTypes];
        return RuleEvaluation.Create(types, () => rule.GetViolationsIn(modules.Architecture));
    }

    private static IReadOnlyList<String> GetViolationsReaching(
        ArchModules modules,
        ArchModule module,
        ArchModule otherModule) =>
        Types()
            .That()
            .ResideInNamespaceMatching(module.RootPattern)
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(otherModule.OutsideContractsPattern)
            .GetViolationsIn(modules.Architecture);

    private static String BuildNamespacePattern(params IEnumerable<String> namespaces) =>
        $"^({String.Join('|', namespaces.Select(Regex.Escape))})(\\..+)?$";

    private static String BuildTypePattern(IEnumerable<String> namespaces, IEnumerable<String> allowedTypeNames)
    {
        var allowedTypes = String.Join('|', allowedTypeNames.Select(Regex.Escape));
        var namespacePrefixes = String.Join('|', namespaces.Select(Regex.Escape));

        return $"^(?!({allowedTypes})$)({namespacePrefixes})\\..+$";
    }
}
