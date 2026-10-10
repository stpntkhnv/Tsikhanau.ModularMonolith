using Acme.Modules.Good.Domain.Widgets.Events;
using Tsikhanau.Monolith.Domain.Aggregates;
using Tsikhanau.Railway;

namespace Acme.Modules.Good.Domain.Widgets;

internal sealed class Widget : AggregateRoot<WidgetId>
{
    private Widget(WidgetId id, WidgetLabel label)
        : base(id)
    {
        Label = label;
    }

    public WidgetLabel Label { get; private set; }

    public static Result<Widget> Create(WidgetId id, WidgetLabel label, DateTimeOffset createdOn)
    {
        var widget = new Widget(id, label);
        widget.Raise(new WidgetCreatedDomainEvent(createdOn, id.Value));
        return widget;
    }

    public void Rename(WidgetLabel label) => Label = label;
}
