namespace Tsikhanau.Monolith.Scaffold.Edits;

internal sealed record JsonText(String Text, String NewLine)
{
    public Int32 GetLineStart(Int32 offset) =>
        Text
            .AsSpan(0, offset)
            .LastIndexOf('\n') + 1;

    public Boolean IsFirstOnLine(Int32 offset) => IsBlank(GetLineStart(offset), offset);

    public Boolean IsBlank(Int32 start, Int32 end) =>
        Text
            .AsSpan(start, end - start)
            .IsWhiteSpace();

    public String ReadIndentation(Int32 offset) => TextLines.ReadIndentation(Text[GetLineStart(offset)..offset]);

    public Boolean HasCommaBetween(Int32 start, Int32 end) => HasComma(Text.AsSpan(start, end - start));

    private static Boolean HasComma(ReadOnlySpan<Char> gap) =>
        gap.TrimStart() switch
        {
            [',', ..] => true,
            ['/', '/', .. var comment] => HasComma(SkipLineComment(comment)),
            ['/', '*', .. var comment] => HasComma(SkipBlockComment(comment)),
            _ => false,
        };

    private static ReadOnlySpan<Char> SkipLineComment(ReadOnlySpan<Char> comment) =>
        comment.IndexOf('\n') is var lineEnd and >= 0 ? comment[lineEnd..] : [];

    private static ReadOnlySpan<Char> SkipBlockComment(ReadOnlySpan<Char> comment) =>
        comment.IndexOf("*/", StringComparison.Ordinal) is var commentEnd and >= 0 ? comment[(commentEnd + 2)..] : [];
}
