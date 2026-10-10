using Acme.Modules.Good.Domain.Widgets;

namespace Acme.Modules.Good.Infrastructure.Persistence;

internal sealed class WidgetRepository(GoodDbContext dbContext) : IWidgetRepository
{
    public void Add(Widget widget) => dbContext.Add(widget);
}
