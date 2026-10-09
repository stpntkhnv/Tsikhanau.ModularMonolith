namespace Tsikhanau.Monolith.ArchRules.Support;

internal static class TypeNameExtensions
{
    extension(Type type)
    {
        public String FullNameOrName => type.FullName ?? type.Name;
    }
}
