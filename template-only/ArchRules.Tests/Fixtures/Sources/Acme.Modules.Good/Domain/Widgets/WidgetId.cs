using Tsikhanau.Monolith.Domain.Ids;

namespace Acme.Modules.Good.Domain.Widgets;

internal readonly record struct WidgetId(Guid Value) : ITypedId<WidgetId>
{
    public static WidgetId From(Guid value) => new(value);
}
