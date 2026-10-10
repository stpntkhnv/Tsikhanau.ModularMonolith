using System.Reflection;
using System.Text.RegularExpressions;
using Tsikhanau.Monolith.Infrastructure.Persistence;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Infrastructure.Modules;

internal sealed partial record ModuleDefinition(String Name, String Schema, Assembly Assembly, String RootNamespace)
{
    private const String ModuleGroup = "Module";

    private const String NameProblem = "must be named I<Module>Module, like INotesModule";

    private const String RootProblem = "must sit at the root of its module, in a namespace ending with";

    public IReadOnlyDictionary<String, Error> ConstraintErrors { get; init; } = new Dictionary<String, Error>();

    public static ModuleDefinition Create<TModule, TDbContext>(IReadOnlyDictionary<String, Error> constraintErrors)
        where TDbContext : ModuleDbContext
    {
        var facadeType = typeof(TModule);
        var name = GetName(facadeType);
        var rootNamespace = GetRootNamespace(facadeType, name);
        var schema = ModuleSchema.Get(typeof(TDbContext));

        return new ModuleDefinition(name, schema, facadeType.Assembly, rootNamespace)
        {
            ConstraintErrors = constraintErrors,
        };
    }

    private static String GetName(Type facadeType)
    {
        var facadeNamePattern = FacadeNamePattern();
        return facadeNamePattern.Match(facadeType.Name) is { Success: true } facadeName
            ? facadeName.Groups[ModuleGroup].Value
            : throw new InvalidOperationException($"Module facade {facadeType.FullName} {NameProblem}");
    }

    private static String GetRootNamespace(Type facadeType, String name) =>
        facadeType is { IsNested: false, Namespace: { } rootNamespace }
            && rootNamespace.EndsWith($".{name}", StringComparison.Ordinal)
            ? rootNamespace
            : throw new InvalidOperationException($"Module facade {facadeType.FullName} {RootProblem} .{name}");

    [GeneratedRegex($@"\AI(?<{ModuleGroup}>\p{{Lu}}\w*)Module\z")]
    private static partial Regex FacadeNamePattern();
}
