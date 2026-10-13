namespace Tsikhanau.Monolith.ArchRules.Support;

internal ref struct SqlTokenizer(ReadOnlySpan<Char> sql)
{
    private ReadOnlySpan<Char> _rest = sql;

    public SqlToken Current { get; private set; }

    public Boolean MoveNext()
    {
        _rest = SkipSpacesAndComments(_rest);
        var length = MeasureToken(_rest);
        Current = new SqlToken(_rest[..length]);
        _rest = _rest[length..];
        return length > 0;
    }

    public Boolean Skip(String word)
    {
        var following = this;
        if (!following.MoveNext() || !following.Current.Is(word))
        {
            return false;
        }

        this = following;
        return true;
    }

    private static ReadOnlySpan<Char> SkipSpacesAndComments(ReadOnlySpan<Char> text)
    {
        var rest = text.TrimStart();
        while (rest is ['-', '-', ..] or ['/', '*', ..])
        {
            rest = rest[MeasureComment(rest)..].TrimStart();
        }

        return rest;
    }

    private static Int32 MeasureComment(ReadOnlySpan<Char> text) =>
        text is ['-', ..] ? MeasureLineComment(text) : MeasureBlockComment(text);

    private static Int32 MeasureLineComment(ReadOnlySpan<Char> text)
    {
        var lineEnd = text.IndexOfAny('\r', '\n');
        return lineEnd < 0 ? text.Length : lineEnd;
    }

    private static Int32 MeasureBlockComment(ReadOnlySpan<Char> text)
    {
        var length = 2;
        while (length < text.Length && text[length..] is not ['*', '/', ..])
        {
            length += text[length..] is ['/', '*', ..] ? MeasureBlockComment(text[length..]) : 1;
        }

        return Math.Min(length + 2, text.Length);
    }

    private static Int32 MeasureToken(ReadOnlySpan<Char> text) =>
        text switch
        {
            [] => 0,
            ['\'', ..] => MeasureQuoted(text, '\''),
            ['"', ..] => MeasureQuoted(text, '"'),
            ['E' or 'e', '\'', ..] => 1 + MeasureEscapeString(text[1..]),
            ['$', ..] => MeasureDollarQuoted(text),
            [var first, ..] when IsWordCharacter(first) => MeasureWord(text),
            _ => 1,
        };

    private static Int32 MeasureQuoted(ReadOnlySpan<Char> text, Char quote)
    {
        var length = MeasureQuotedSegment(text, quote);
        while (text[length..].StartsWith(quote))
        {
            length += MeasureQuotedSegment(text[length..], quote);
        }

        return length;
    }

    private static Int32 MeasureQuotedSegment(ReadOnlySpan<Char> text, Char quote)
    {
        var closingQuote = text[1..].IndexOf(quote);
        return closingQuote < 0 ? text.Length : closingQuote + 2;
    }

    private static Int32 MeasureEscapeString(ReadOnlySpan<Char> text)
    {
        var length = 1;
        while (length < text.Length)
        {
            var rest = text[length..];
            if (rest is ['\''] or ['\'', not '\'', ..])
            {
                return length + 1;
            }

            length += rest is ['\\', ..] or ['\'', '\'', ..] ? 2 : 1;
        }

        return text.Length;
    }

    private static Int32 MeasureDollarQuoted(ReadOnlySpan<Char> text)
    {
        var tagLength = MeasureDollarTag(text);
        if (tagLength == 0)
        {
            return 1;
        }

        var closingTag = text[tagLength..].IndexOf(text[..tagLength]);
        return closingTag < 0 ? text.Length : tagLength + closingTag + tagLength;
    }

    private static Int32 MeasureDollarTag(ReadOnlySpan<Char> text)
    {
        var name = text[1..];
        var nameLength = name.IndexOf('$');
        return nameLength >= 0 && IsDollarTagName(name[..nameLength]) ? nameLength + 2 : 0;
    }

    private static Boolean IsDollarTagName(ReadOnlySpan<Char> name) =>
        name is not [>= '0' and <= '9', ..] && MeasureWord(name) == name.Length;

    private static Int32 MeasureWord(ReadOnlySpan<Char> text)
    {
        var length = 0;
        while (length < text.Length && IsWordCharacter(text[length]))
        {
            length++;
        }

        return length;
    }

    private static Boolean IsWordCharacter(Char character) =>
        Char.IsLetterOrDigit(character) || character is '_' or '$';
}
