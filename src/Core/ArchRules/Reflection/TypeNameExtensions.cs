namespace Tsikhanau.Monolith.ArchRules.Reflection;

internal static class TypeNameExtensions
{
    extension(Type type)
    {
        public String FullNameOrName => type.FullName ?? type.Name;
    }
}
