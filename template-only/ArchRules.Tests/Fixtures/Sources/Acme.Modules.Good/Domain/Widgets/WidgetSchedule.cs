namespace Acme.Modules.Good.Domain.Widgets;

internal static class WidgetSchedule
{
    public static Boolean IsDue(DateTimeOffset scheduledOn, TimeSpan delay, DateTimeOffset checkedOn) =>
        checkedOn >= scheduledOn + delay;
}
