using System.Reflection;
using Tsikhanau.Monolith.Application.Modules;
using Tsikhanau.Monolith.Infrastructure.Persistence;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Infrastructure.Modules;

internal sealed record ModuleDefinition(String Name, String Schema, Assembly Assembly, String RootNamespace)
{
    private const String RootProblem = "must sit at the root of its module, in a namespace ending with";

    public IReadOnlyDictionary<String, Error> ConstraintErrors { get; init; } = new Dictionary<String, Error>();

    public static ModuleDefinition Create<TModule, TDbContext>(IReadOnlyDictionary<String, Error> constraintErrors)
        where TDbContext : ModuleDbContext
    {
        var facadeType = typeof(TModule);
        var name = ModuleName.FromFacade(facadeType);
        var rootNamespace = GetRootNamespace(facadeType, name);
        var schema = ModuleSchema.Get(typeof(TDbContext));

        return new ModuleDefinition(name, schema, facadeType.Assembly, rootNamespace)
        {
            ConstraintErrors = constraintErrors,
        };
    }

    private static String GetRootNamespace(Type facadeType, String name) =>
        facadeType is { IsNested: false, Namespace: { } rootNamespace }
            && rootNamespace.EndsWith($".{name}", StringComparison.Ordinal)
            ? rootNamespace
            : throw new InvalidOperationException($"Module facade {facadeType.FullName} {RootProblem} .{name}");
}
