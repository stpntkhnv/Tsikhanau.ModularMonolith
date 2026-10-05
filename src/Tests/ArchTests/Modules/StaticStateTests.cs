using System.Reflection;
using ModularMonolith.ArchTests.Support;

namespace ModularMonolith.ArchTests.Modules;

public sealed class StaticStateTests
{
    private const BindingFlags StaticMembers =
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    [Theory]
    [EveryModule]
    public void Module_types_hold_no_static_mutable_state(String name)
    {
        var types = ArchModules
            .Get(name).Types
            .Where(type => !type.IsCompilerGenerated)
            .ToList();

        Assert.SkipWhen(types.Count == 0, $"{name} has no types");

        types
            .SelectMany(GetStaticMutableMembers)
            .ShouldBeEmpty("Keep state in instances that DI owns; static state leaks between requests and tests");
    }

    [Theory]
    [EveryModule]
    public void Domain_types_have_no_public_fields(String name)
    {
        var domainTypes = ArchModules
            .Get(name)
            .GetTypesIn("Domain")
            .Where(type => !type.IsCompilerGenerated)
            .ToList();

        Assert.SkipWhen(domainTypes.Count == 0, $"{name} has no types in Domain");

        domainTypes
            .SelectMany(type => type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Where(field => !field.IsSpecialName)
            .Select(field => $"{field.DeclaringType!.FullName}.{field.Name}")
            .ShouldBeEmpty("Expose domain state through read-only properties");
    }

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

        return [.. fields, .. properties];
    }
}
