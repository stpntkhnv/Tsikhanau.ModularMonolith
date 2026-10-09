using System.Reflection;
using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;
using Tsikhanau.Monolith.ArchRules.Support;

namespace Tsikhanau.Monolith.ArchRules.Modules;

public static class StaticStateRules
{
    private const BindingFlags StaticMembers =
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    public static RuleEvaluation ModuleTypesHoldNoStaticMutableState(ArchModule module) =>
        module.Types
            .Where(type => !type.IsCompilerGenerated)
            .CollectViolations(GetStaticMutableMembers);

    public static RuleEvaluation DomainTypesHaveNoPublicFields(ArchModule module) =>
        module
            .GetTypesIn("Domain")
            .Where(type => !type.IsCompilerGenerated)
            .CollectViolations(GetPublicFields);

    private static IEnumerable<String> GetStaticMutableMembers(Type type)
    {
        var fields = type
            .GetFields(StaticMembers)
            .Where(field => field is { IsInitOnly: false, IsLiteral: false } && !field.IsCompilerGenerated)
            .Select(field => $"{type.FullName}.{field.Name}");

        var properties = type
            .GetProperties(StaticMembers)
            .Where(property => property.SetMethod is not null)
            .Select(property => $"{type.FullName}.{property.Name}");

        var events = type
            .GetEvents(StaticMembers)
            .Select(staticEvent => $"{type.FullName}.{staticEvent.Name} (static event)");

        return [.. fields, .. properties, .. events];
    }

    private static IEnumerable<String> GetPublicFields(Type type) =>
        type
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(field => !field.IsSpecialName)
            .Select(field => $"{field.DeclaringType!.FullName}.{field.Name}");
}
