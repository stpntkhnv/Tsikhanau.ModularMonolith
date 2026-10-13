using Acme.Modules.Good.Domain.Widgets;
using Acme.SharedKernel;
using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Railway;

namespace Acme.Modules.Good.Features.CreateWidget;

internal sealed class CreateWidgetCommandHandler(IWidgetRepository widgetRepository, TimeProvider timeProvider)
    : ICommandHandler<CreateWidgetCommand, Guid>
{
    public Task<Result<Guid>> Handle(CreateWidgetCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(
            WidgetLabel
                .Create(command.Label, nameof(command.Label))
                .Bind(label => CreateWidget(command, label))
                .Map(Add));

    private Result<Widget> CreateWidget(CreateWidgetCommand command, WidgetLabel label)
    {
        var createdOn = timeProvider.GetUtcNow();
        var widgetId = WidgetId.From(command.WidgetId.GetValueOrDefault(() => Guid.CreateVersion7(createdOn)));

        return Money
            .Create(command.Price, nameof(command.Price))
            .Bind(price => Widget.Create(widgetId, label, price, createdOn));
    }

    private Guid Add(Widget widget)
    {
        widgetRepository.Add(widget);
        return widget.Id.Value;
    }
}
