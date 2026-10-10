namespace Tsikhanau.Monolith.Scaffold.Edits;

internal sealed record TextLines(IReadOnlyList<String> Lines, String NewLine)
{
    public static TextLines Split(String text, String newLine) => new(text.Split(newLine), newLine);

    public String Join() => String.Join(NewLine, Lines);

    public TextLines Insert(Int32 index, params IEnumerable<String> lines) =>
        this with { Lines = [.. Lines.Take(index), .. lines, .. Lines.Skip(index)] };

    public Boolean Contains(String trimmedLine) => Lines.Any(line => line.Trim() == trimmedLine);

    public IReadOnlyList<Int32> GetIndexes(Func<String, Boolean> predicate) =>
        [
            .. Lines
                .Select((line, index) => (Line: line, Index: index))
                .Where(entry => predicate(entry.Line))
                .Select(entry => entry.Index),
        ];

    public static String ReadIndentation(String line) => line[..(line.Length - line.TrimStart().Length)];
}
