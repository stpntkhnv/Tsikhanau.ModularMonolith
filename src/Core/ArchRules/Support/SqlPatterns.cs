using System.Text.RegularExpressions;

namespace Tsikhanau.Monolith.ArchRules.Support;

internal static partial class SqlPatterns
{
    private const String WritingStatementPattern =
        """\b(INSERT\s+INTO|UPDATE\s+[\w."]+(\s+(AS\s+)?[\w."]+)?\s+SET|DELETE\s+FROM|MERGE\s+INTO"""
            + """|TRUNCATE\s+(TABLE\s+)?(ONLY\s+)?[\w."]+)\b""";

    private const String StarColumnPattern =
        """\b(SELECT|RETURNING)\s+(DISTINCT\s+)?([\w."]+\s*,\s*)*([\w."]+\.)?\*""";

    [GeneratedRegex(WritingStatementPattern, RegexOptions.IgnoreCase)]
    public static partial Regex WritingStatement { get; }

    [GeneratedRegex(StarColumnPattern, RegexOptions.IgnoreCase)]
    public static partial Regex StarColumn { get; }

    public static Boolean NamesSchema(String sql, String schema) =>
        Regex.IsMatch(
            sql,
            $$"""(?<![\w."])"?{{Regex.Escape(schema)}}"?\."?[a-z_][a-z0-9_]*\b(?!-\w)""",
            RegexOptions.IgnoreCase);
}
