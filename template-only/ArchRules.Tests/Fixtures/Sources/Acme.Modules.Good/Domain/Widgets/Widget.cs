using Acme.Modules.Good.Domain.Widgets.Events;
using Acme.SharedKernel;
using Tsikhanau.Monolith.Domain.Aggregates;
using Tsikhanau.Monolith.Shared.Collections;
using Tsikhanau.Railway;

namespace Acme.Modules.Good.Domain.Widgets;

internal sealed class Widget : AggregateRoot<WidgetId>
{
    private Widget(WidgetId id, WidgetLabel label, Money price)
        : base(id)
    {
        Label = label;
        Price = price;
    }

    public WidgetLabel Label { get; private set; }

    public Money Price { get; private set; }

    public static Result<Widget> Create(WidgetId id, WidgetLabel label, Money price, DateTimeOffset createdOn)
    {
        var widget = new Widget(id, label, price);
        widget.Raise(new WidgetCreatedDomainEvent(createdOn, id.Value));
        return widget;
    }

    public void Rename(WidgetLabel label) => Label = label;

    public static void RenameAll(IEnumerable<Widget> widgets, WidgetLabel label) =>
        widgets.ForEach(widget => widget.Rename(label));
}
