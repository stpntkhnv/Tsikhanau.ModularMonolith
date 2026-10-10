using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Edits;

internal sealed record JsonObjectOutline(Int32 Start, Int32 End, IReadOnlyList<JsonPropertyOutline> Properties)
{
    public Boolean IsEmpty => Properties.Count == 0;

    public Optional<JsonPropertyOutline> FindProperty(String name) =>
        Properties.FirstOrDefault(property => property.HasName(name));

    public Optional<JsonObjectOutline> FindObject(String name) =>
        FindProperty(name)
            .Bind(property => property.ObjectValue);
}
