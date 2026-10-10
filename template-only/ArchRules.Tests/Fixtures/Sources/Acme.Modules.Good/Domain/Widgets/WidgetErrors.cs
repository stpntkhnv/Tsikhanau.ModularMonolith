using Tsikhanau.Railway;

namespace Acme.Modules.Good.Domain.Widgets;

internal static class WidgetErrors
{
    public static readonly Error NotFound = Error.NotFound("Widget.NotFound", "Widget was not found");
}
