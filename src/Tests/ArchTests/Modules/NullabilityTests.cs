using System.Reflection;
using ModularMonolith.ArchTests.Support;
using Tsikhanau.Monolith.Shared.Reflection;

namespace ModularMonolith.ArchTests.Modules;

public sealed class NullabilityTests
{
    private const BindingFlags DeclaredMembers =
        BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
        | BindingFlags.DeclaredOnly;

    [Theory]
    [EveryModule]
    public void Domain_and_feature_classes_return_optional_instead_of_null(String name)
    {
        var module = ArchModules.Get(name);
        var subjects = module.Types
            .Where(type => !type.IsCompilerGenerated)
            .Where(type => module.Contains(type, "Domain") || IsFeatureClass(module, type))
            .ToList();

        Assert.SkipWhen(subjects.Count == 0, $"{name} has no Domain or Features types");

        subjects
            .SelectMany(GetNullableReturns)
            .ShouldBeEmpty("Return Optional<T> for a value that may be absent");
    }

    private static Boolean IsFeatureClass(ArchModule module, Type type) =>
        module.Contains(type, "Features") && type is { IsClass: true } && !type.IsRecord;

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

    private static Boolean IsNullable(NullabilityInfo nullabilityInfo) =>
        nullabilityInfo.ReadState == NullabilityState.Nullable
            || (IsAwaitable(nullabilityInfo.Type)
                && nullabilityInfo.GenericTypeArguments[0].ReadState == NullabilityState.Nullable);

    private static Boolean IsAwaitable(Type type) =>
        type.IsBuiltFrom(typeof(Task<>)) || type.IsBuiltFrom(typeof(ValueTask<>));
}
