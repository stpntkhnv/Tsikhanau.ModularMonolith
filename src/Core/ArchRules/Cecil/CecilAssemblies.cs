using System.Reflection;
using Mono.Cecil;

namespace Tsikhanau.Monolith.ArchRules.Cecil;

internal static class CecilAssemblies
{
    public static IReadOnlyList<T> Read<T>(
        IEnumerable<Assembly> assemblies,
        Func<TypeDefinition, Boolean> isSubject,
        Func<TypeDefinition, IEnumerable<T>> read) =>
    [
        .. assemblies
            .Select(assembly => assembly.Location)
            .Distinct()
            .SelectMany(location => Read(location, isSubject, read)),
    ];

    private static List<T> Read<T>(
        String location,
        Func<TypeDefinition, Boolean> isSubject,
        Func<TypeDefinition, IEnumerable<T>> read)
    {
        using var assembly = AssemblyDefinition.ReadAssembly(location);
        return assembly.MainModule
            .GetTypes()
            .Where(isSubject)
            .SelectMany(read)
            .ToList();
    }
}
