namespace Tsikhanau.Monolith.Scaffold.Events;

internal sealed record MemberSample(String Expression, IReadOnlyList<String> Usings)
{
    public static MemberSample Of(String expression, params IReadOnlyList<String> usings) => new(expression, usings);
}
