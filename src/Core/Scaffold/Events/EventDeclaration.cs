namespace Tsikhanau.Monolith.Scaffold.Events;

internal sealed record EventDeclaration(
    String Name,
    String Namespace,
    EventKind Kind,
    String OwnerModule,
    IReadOnlyList<MemberSample> Members)
{
    public String HandlerSuffix => $"{Kind}EventHandler";

    public String HandlerInterface => $"I{HandlerSuffix}";

    public String ParameterName => Kind == EventKind.Domain ? "domainEvent" : "integrationEvent";

    public IReadOnlyList<String> Usings =>
        [
            .. Members
                .SelectMany(member => member.Usings)
                .Prepend(Namespace)
                .Distinct(StringComparer.Ordinal),
        ];
}
