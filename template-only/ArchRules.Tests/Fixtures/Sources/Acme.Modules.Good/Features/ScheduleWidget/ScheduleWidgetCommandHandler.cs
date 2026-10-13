using Acme.Modules.Good.Features.TouchWidget;
using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Railway;

namespace Acme.Modules.Good.Features.ScheduleWidget;

internal sealed class ScheduleWidgetCommandHandler(
    IInternalCommandScheduler<IGoodModule> internalCommandScheduler,
    TimeProvider timeProvider)
    : ICommandHandler<ScheduleWidgetCommand, Unit>
{
    public Task<Result<Unit>> Handle(ScheduleWidgetCommand command, CancellationToken cancellationToken)
    {
        var createdOn = timeProvider.GetUtcNow();
        internalCommandScheduler.Enqueue(new TouchWidgetInternalCommand(Guid.CreateVersion7(createdOn), command.WidgetId));

        return Task.FromResult<Result<Unit>>(Unit.Value);
    }
}
