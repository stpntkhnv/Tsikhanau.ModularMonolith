namespace Tsikhanau.Monolith.Scaffold.Plans;

internal sealed record ExternalStep(String FileName, IReadOnlyList<String> Arguments)
{
    public override String ToString() => String.Join(' ', [FileName, .. Arguments]);
}
