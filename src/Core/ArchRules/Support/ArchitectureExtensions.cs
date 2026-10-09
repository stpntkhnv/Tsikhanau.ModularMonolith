using System.Reflection;
using ArchUnitNET.Loader;
using Architecture = ArchUnitNET.Domain.Architecture;

namespace Tsikhanau.Monolith.ArchRules.Support;

internal static class ArchitectureExtensions
{
    extension(IEnumerable<Assembly> assemblies)
    {
        public Architecture LoadArchitecture() =>
            new ArchLoader()
                .LoadAssemblies([.. assemblies])
                .Build();
    }
}
