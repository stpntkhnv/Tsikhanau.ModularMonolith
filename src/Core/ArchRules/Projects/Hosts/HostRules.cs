using System.Reflection;
using Mono.Cecil;
using Tsikhanau.Monolith.Api.Modules;
using Tsikhanau.Monolith.ArchRules.Evaluation;
using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Reflection;

namespace Tsikhanau.Monolith.ArchRules.Projects.Hosts;

public static class HostRules
{
    public static RuleEvaluation HostAddsEveryModuleFolder(Assembly hostAssembly, ArchModules modules)
    {
        var addedNames = ReadModulesAddedBy(hostAssembly);
        IReadOnlyList<String> expectedNames = [.. modules.All.Select(module => module.ModuleClassName)];
        IReadOnlyList<String> subject = [.. expectedNames.Union(addedNames)];

        return RuleEvaluation.Create(subject, () => GetViolations(hostAssembly.Name, modules.All, addedNames));
    }

    private static IEnumerable<String> GetViolations(
        String hostName,
        IReadOnlyList<ArchModule> modules,
        IReadOnlyList<String> addedNames)
    {
        var missingModules = modules
            .Where(module => !addedNames.Contains(module.ModuleClassName))
            .Select(module => $"{hostName} does not add src/Modules/{module.Name} ({module.ModuleClassName})");

        var strayNames = addedNames
            .Except(modules.Select(module => module.ModuleClassName))
            .Select(name => $"{hostName} adds {name}, which is not in src/Modules");

        var repeatedNames = addedNames
            .GroupBy(name => name)
            .Where(group => group.Count() > 1)
            .Select(group => $"{hostName} adds {group.Key} more than once");

        return [.. missingModules, .. strayNames, .. repeatedNames];
    }

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
