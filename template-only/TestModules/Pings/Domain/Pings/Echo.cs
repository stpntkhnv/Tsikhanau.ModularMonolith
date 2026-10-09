using Tsikhanau.Monolith.Domain.Aggregates;

namespace Tsikhanau.Monolith.TestModules.Pings.Domain.Pings;

internal sealed class Echo : Entity<EchoId>
{
    private Echo(EchoId id, String text) : base(id) => Text = text;

    public String Text { get; private set; }

    internal static Echo Create(EchoId id, String text) => new(id, text);
}
