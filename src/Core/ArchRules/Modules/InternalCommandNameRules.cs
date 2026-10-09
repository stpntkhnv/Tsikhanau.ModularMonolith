using System.Reflection;
using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;
using Tsikhanau.Monolith.Shared.Reflection;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.ArchRules.Modules;

public static class InternalCommandNameRules
{
    public static RuleEvaluation EveryInternalCommandHasAStableNameUniqueInItsModule(ArchModules modules)
    {
        IReadOnlyList<Type> commandTypes = [.. modules.All.SelectMany(GetCommandTypes)];
        return RuleEvaluation.Create(commandTypes, () => [.. GetUnnamed(commandTypes), .. GetSharedNames(modules)]);
    }

    private static IEnumerable<Type> GetCommandTypes(ArchModule module) =>
        module.Types.Implementing<InternalCommand>();

    private static IEnumerable<String> GetUnnamed(IEnumerable<Type> commandTypes) =>
        commandTypes
            .Where(type => FindCommandName(type).IsNone)
            .Select(type => $"{type.FullName} has no [InternalCommandName] with a name");

    private static IEnumerable<String> GetSharedNames(ArchModules modules) =>
        modules.All.SelectMany(GetSharedNames);

    private static IEnumerable<String> GetSharedNames(ArchModule module) =>
        GetCommandTypes(module)
            .Select(FindCommandName)
            .Where(name => name.HasValue)
            .GroupBy(name => name.Value)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key} names more than one internal command of {module.Name}");

    private static Optional<String> FindCommandName(Type type) =>
        Optional<String>
            .FromNullable(type.GetCustomAttribute<InternalCommandNameAttribute>()?.Name)
            .Where(name => !String.IsNullOrWhiteSpace(name));
}
