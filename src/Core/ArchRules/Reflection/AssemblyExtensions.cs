using System.Reflection;

namespace Tsikhanau.Monolith.ArchRules.Reflection;

internal static class AssemblyExtensions
{
    extension(Assembly assembly)
    {
        public String Name => assembly.GetName().Name!;
    }
}
