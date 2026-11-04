using System.Text.RegularExpressions;

namespace Tsikhanau.Monolith.Scaffold.Names;

internal static partial class PascalCase
{
    public const String Rule = "must be PascalCase: an ASCII capital letter, then letters and digits";

    [GeneratedRegex(@"\A[A-Z][A-Za-z0-9]*\z")]
    private static partial Regex Pattern { get; }

    public static Boolean Matches(String name) => Pattern.IsMatch(name);
}
