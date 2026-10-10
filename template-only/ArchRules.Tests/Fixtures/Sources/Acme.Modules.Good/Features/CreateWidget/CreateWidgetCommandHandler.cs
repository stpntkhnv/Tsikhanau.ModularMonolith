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

    private Result<Widget> CreateWidget(CreateWidgetCommand command, WidgetLabel label) =>
        Money
            .Create(command.Price, nameof(command.Price))
            .Bind(price => Widget.Create(WidgetId.From(command.WidgetId), label, price, timeProvider.GetUtcNow()));

    private Guid Add(Widget widget)
    {
        widgetRepository.Add(widget);
        return widget.Id.Value;
    }
}
