using System.Reflection;
using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;
using Tsikhanau.Monolith.ArchRules.Support;
using Tsikhanau.Monolith.Contracts.IntegrationEvents;
using Tsikhanau.Monolith.Shared.Reflection;

namespace Tsikhanau.Monolith.ArchRules.Modules;

public static class NullabilityRules
{
    private const BindingFlags DeclaredMembers =
        BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
        | BindingFlags.DeclaredOnly;

    private const BindingFlags PublicInstanceMembers = BindingFlags.Instance | BindingFlags.Public;

    public static RuleEvaluation DomainAndFeatureClassesReturnOptionalInsteadOfNull(ArchModule module) =>
        module.Types
            .Where(type => !type.IsCompilerGenerated)
            .Where(type => module.Contains(type, "Domain") || IsFeatureClass(module, type))
            .CollectViolations(GetNullableReturns);

    public static RuleEvaluation MessagesAndRequestsCarryOptionalInsteadOfNull(ArchModule module) =>
        module.Types
            .Where(type => type.IsConcrete)
            .Where(type => IsMessage(type) || IsRequest(module, type))
            .CollectViolations(GetNullableProperties);

    private static Boolean IsFeatureClass(ArchModule module, Type type) =>
        module.Contains(type, "Features") && type is { IsClass: true } && !type.IsRecord;

    private static Boolean IsMessage(Type type) =>
        type.Implements<IntegrationEvent>() || type.Implements(typeof(ICommand<>));

    private static Boolean IsRequest(ArchModule module, Type type) =>
        module.Contains(type, "Features") && type.IsRecord && type.Name.EndsWith("Request", StringComparison.Ordinal);

    private static IEnumerable<String> GetNullableReturns(Type type)
    {
        var nullabilityInfoContext = new NullabilityInfoContext();
        var methods = type
            .GetMethods(DeclaredMembers)
            .Where(method => !method.IsSpecialName && !method.IsCompilerGenerated)
            .Where(method => IsNullable(nullabilityInfoContext.Create(method.ReturnParameter)))
            .Select(method => $"{type.FullName}.{method.Name}");

        var properties = type
            .GetProperties(DeclaredMembers)
            .Where(property => !property.IsCompilerGenerated)
            .Where(property => IsNullable(nullabilityInfoContext.Create(property)))
            .Select(property => $"{type.FullName}.{property.Name}");

        return [.. methods, .. properties];
    }

    private static IEnumerable<String> GetNullableProperties(Type type)
    {
        var nullabilityInfoContext = new NullabilityInfoContext();
        return type
            .GetProperties(PublicInstanceMembers)
            .Where(property => nullabilityInfoContext.Create(property).ReadState == NullabilityState.Nullable)
            .Select(property => $"{type.FullName}.{property.Name}");
    }

    private static Boolean IsNullable(NullabilityInfo nullabilityInfo) =>
        nullabilityInfo.ReadState == NullabilityState.Nullable
            || (IsAwaitable(nullabilityInfo.Type)
                && nullabilityInfo.GenericTypeArguments[0].ReadState == NullabilityState.Nullable);

    private static Boolean IsAwaitable(Type type) =>
        type.IsBuiltFrom(typeof(Task<>)) || type.IsBuiltFrom(typeof(ValueTask<>));
}
