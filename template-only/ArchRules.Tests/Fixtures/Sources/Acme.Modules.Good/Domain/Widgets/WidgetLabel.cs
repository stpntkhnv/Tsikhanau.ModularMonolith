using Tsikhanau.Monolith.Domain.ValueObjects;
using Tsikhanau.Railway;

namespace Acme.Modules.Good.Domain.Widgets;

internal readonly record struct WidgetLabel : IValueObject<WidgetLabel, String>
{
    private WidgetLabel(String value) => Value = value;

    public String Value { get; }

    static WidgetLabel IValueObject<WidgetLabel, String>.From(String value) => new(value);

    public static Result<WidgetLabel> Create(String value, String fieldName) =>
        String.IsNullOrWhiteSpace(value) ? Error.Validation(fieldName, "Label is required") : new WidgetLabel(value);
}
