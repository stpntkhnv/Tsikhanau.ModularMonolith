using System.Reflection;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Infrastructure.Modules;

public sealed record ModuleDefinition(String Name, String Schema, Assembly Assembly, String RootNamespace)
{
    public IReadOnlyDictionary<String, Error> ConstraintErrors { get; init; } = new Dictionary<String, Error>();
}
