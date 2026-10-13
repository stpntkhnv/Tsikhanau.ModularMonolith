namespace Tsikhanau.Monolith.ArchRules.Support;

internal static class SqlScanner
{
    private static readonly String[] ColumnListEnds =
    [
        "FROM",
        "INTO",
        "WHERE",
        "GROUP",
        "HAVING",
        "WINDOW",
        "ORDER",
        "LIMIT",
        "OFFSET",
        "FETCH",
        "FOR",
        "UNION",
        "INTERSECT",
        "EXCEPT",
        ";",
    ];

    public static Boolean ReadsEveryColumn(String sql) =>
        sql.Contains('*') && HasStarColumn(sql);

    private static Boolean HasStarColumn(String sql)
    {
        var tokens = new SqlTokenizer(sql);
        while (tokens.MoveNext())
        {
            if (OpensColumnList(tokens.Current) && ColumnListHasStar(tokens))
            {
                return true;
            }
        }

        return false;
    }

    private static Boolean OpensColumnList(SqlToken token) =>
        token.Is("SELECT") || token.Is("RETURNING");

    private static Boolean ColumnListHasStar(SqlTokenizer tokens)
    {
        SkipSetQuantifier(ref tokens);
        var column = ReadColumn(ref tokens);
        while (!column.IsStar && !column.EndsList)
        {
            column = ReadColumn(ref tokens);
        }

        return column.IsStar;
    }

    private static void SkipSetQuantifier(ref SqlTokenizer tokens)
    {
        if (tokens.Skip("ALL"))
        {
            return;
        }

        if (tokens.Skip("DISTINCT") && tokens.Skip("ON"))
        {
            SkipGroup(ref tokens);
        }
    }

    private static void SkipGroup(ref SqlTokenizer tokens)
    {
        var depth = 0;
        while (tokens.MoveNext())
        {
            depth += tokens.Current.DepthChange;
            if (depth <= 0)
            {
                return;
            }
        }
    }

    private static (Boolean IsStar, Boolean EndsList) ReadColumn(ref SqlTokenizer tokens)
    {
        var depth = 0;
        var isStar = false;
        var starMayFollow = true;
        while (tokens.MoveNext())
        {
            var token = tokens.Current;
            depth += token.DepthChange;
            if (depth < 0 || (depth == 0 && token.IsOneOf(ColumnListEnds)))
            {
                return (isStar, EndsList: true);
            }

            if (depth == 0 && token.Is(","))
            {
                return (isStar, EndsList: false);
            }

            isStar = starMayFollow && token.Is("*");
            starMayFollow = token.Is(".");
        }

        return (isStar, EndsList: true);
    }
}
