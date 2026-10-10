using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Edits;

internal sealed record JsonPropertyOutline(
    String Name,
    Int32 NameStart,
    Int32 ValueEnd,
    Optional<JsonObjectOutline> ObjectValue)
{
    public Boolean HasName(String name) => Name.Equals(name, StringComparison.OrdinalIgnoreCase);
}
