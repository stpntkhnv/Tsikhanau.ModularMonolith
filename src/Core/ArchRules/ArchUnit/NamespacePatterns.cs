using System.Text.RegularExpressions;

namespace Tsikhanau.Monolith.ArchRules.ArchUnit;

internal static class NamespacePatterns
{
    public static String Build(params IEnumerable<String> namespaces) =>
        $"^({String.Join('|', namespaces.Select(Regex.Escape))})(\\..+)?$";
}
