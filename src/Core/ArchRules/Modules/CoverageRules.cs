using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;
using Tsikhanau.Monolith.ArchRules.Support;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.ArchRules.Modules;

public static class CoverageRules
{
    public static RuleEvaluation ProductionModuleLoadsItsProjectAndItsContracts(ArchModule module) =>
        module.LoadFailure.Match(
            failure => new RuleEvaluation([module.Name], [$"{module.Name} cannot load its assemblies: {failure}"]),
            () => EvaluateLoadedAssemblies(module));

    private static RuleEvaluation EvaluateLoadedAssemblies(ArchModule module)
    {
        IReadOnlyList<String> ownAssemblyNames = [.. module.OwnAssemblies.Select(assembly => assembly.Name)];
        return RuleEvaluation.Create(ownAssemblyNames, () => GetLoadingViolations(module));
    }

    private static IEnumerable<String> GetLoadingViolations(ArchModule module)
    {
        String[] expectedNames = [module.RootNamespace, $"{module.RootNamespace}.Contracts"];
        var loadedNames = module.Assemblies
            .Select(assembly => assembly.Name)
            .ToList();

        var missingNames = expectedNames
            .Except(loadedNames)
            .Select(name => $"{module.Name} does not load {name}");

        var strayNames = loadedNames
            .Except(expectedNames)
            .Select(name => $"{module.Name} loads {name}, which is neither its project nor its contracts");

        return [.. missingNames, .. strayNames];
    }
}
