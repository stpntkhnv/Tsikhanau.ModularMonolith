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
        if (tokens.TrySkip("ALL"))
        {
            return;
        }

        if (tokens.TrySkip("DISTINCT") && tokens.TrySkip("ON"))
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
        var callHasStar = false;
        UInt64 subqueryParens = 0;
        var previousIsName = false;
        SqlToken previousToken = default;
        while (tokens.MoveNext())
        {
            var token = tokens.Current;
            depth += token.DepthChange;
            if (depth < 0 || (depth == 0 && EndsColumnList(token, previousToken, previousIsName)))
            {
                return (isStar || callHasStar, EndsList: true);
            }

            if (depth == 0 && token.Is(","))
            {
                return (isStar || callHasStar, EndsList: false);
            }

            subqueryParens = TrackSubqueries(subqueryParens, depth, token, previousToken);
            callHasStar |= token.Is("*") && previousToken.Is(".") && depth > 0 && !InSubquery(subqueryParens, depth);
            isStar = token.Is("*") && (previousToken.IsEmpty || previousToken.Is("."));
            previousIsName = !previousIsName && IntroducesName(previousToken);
            previousToken = token;
        }

        return (isStar || callHasStar, EndsList: true);
    }

    private static UInt64 TrackSubqueries(UInt64 subqueryParens, Int32 depth, SqlToken token, SqlToken previousToken)
    {
        if (depth is < 1 or > 64)
        {
            return subqueryParens;
        }

        var bit = 1UL << (depth - 1);
        if (token.Is("("))
        {
            return subqueryParens & ~bit;
        }

        return previousToken.Is("(") && (token.Is("SELECT") || token.Is("WITH") || token.Is("VALUES"))
            ? subqueryParens | bit
            : subqueryParens;
    }

    private static Boolean InSubquery(UInt64 subqueryParens, Int32 depth) =>
        (subqueryParens & EnclosingParens(depth)) != 0;

    private static UInt64 EnclosingParens(Int32 depth) => depth >= 64 ? UInt64.MaxValue : (1UL << depth) - 1;

    private static Boolean EndsColumnList(SqlToken token, SqlToken previousToken, Boolean previousIsName) =>
        token.IsOneOf(ColumnListEnds) && !ContinuesColumn(token, previousToken, previousIsName);

    private static Boolean ContinuesColumn(SqlToken token, SqlToken previousToken, Boolean previousIsName) =>
        (!previousIsName && IntroducesName(previousToken))
            || (token.Is("FROM") && previousToken.Is("DISTINCT"))
            || (token.Is("GROUP") && previousToken.Is("WITHIN"));

    private static Boolean IntroducesName(SqlToken token) =>
        token.Is(".") || token.Is("AS");
}
