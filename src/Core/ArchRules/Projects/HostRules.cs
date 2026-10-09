using System.Reflection;
using Mono.Cecil;
using Tsikhanau.Monolith.Api.Modules;
using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;
using Tsikhanau.Monolith.ArchRules.Support;

namespace Tsikhanau.Monolith.ArchRules.Projects;

public static class HostRules
{
    public static RuleEvaluation HostAddsEveryModuleFolder(Assembly hostAssembly, ArchModules modules)
    {
        var addedNames = ReadModulesAddedBy(hostAssembly);
        IReadOnlyList<String> expectedNames = [.. modules.All.Select(BuildModuleClassName)];
        IReadOnlyList<String> subject = [.. expectedNames.Union(addedNames)];

        return RuleEvaluation.Create(subject, () => GetViolations(hostAssembly.Name, expectedNames, addedNames));
    }

    private static IEnumerable<String> GetViolations(
        String hostName,
        IReadOnlyList<String> expectedNames,
        IReadOnlyList<String> addedNames)
    {
        var missingNames = expectedNames
            .Except(addedNames)
            .Select(name => $"{hostName} does not add {name}");

        var strayNames = addedNames
            .Except(expectedNames)
            .Select(name => $"{hostName} adds {name}, which is not in src/Modules");

        var repeatedNames = addedNames
            .GroupBy(name => name)
            .Where(group => group.Count() > 1)
            .Select(group => $"{hostName} adds {group.Key} more than once");

        return [.. missingNames, .. strayNames, .. repeatedNames];
    }

    private static String BuildModuleClassName(ArchModule module) => $"{module.RootNamespace}.{module.Name}Module";

    private static IReadOnlyList<String> ReadModulesAddedBy(Assembly hostAssembly)
    {
        using var assembly = AssemblyDefinition.ReadAssembly(hostAssembly.Location);
        return assembly.MainModule
            .GetTypes()
            .SelectMany(type => type.Methods)
            .Where(method => method.HasBody)
            .SelectMany(method => method.Body.Instructions)
            .Select(instruction => instruction.Operand)
            .OfType<GenericInstanceMethod>()
            .Where(calledMethod => calledMethod.Name == nameof(ModuleHostExtensions.AddModule))
            .Select(calledMethod => calledMethod.GenericArguments.Single().FullName)
            .ToList();
    }
}
