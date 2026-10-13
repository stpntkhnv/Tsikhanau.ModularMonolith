namespace Tsikhanau.Monolith.ArchRules.Support;

internal readonly ref struct SqlToken(ReadOnlySpan<Char> text)
{
    private readonly ReadOnlySpan<Char> _text = text;

    public Int32 DepthChange =>
        _text switch
        {
            "(" => 1,
            ")" => -1,
            _ => 0,
        };

    public Boolean Is(String value) =>
        _text.Equals(value, StringComparison.OrdinalIgnoreCase);

    public Boolean IsOneOf(ReadOnlySpan<String> values)
    {
        foreach (var value in values)
        {
            if (Is(value))
            {
                return true;
            }
        }

        return false;
    }
}
