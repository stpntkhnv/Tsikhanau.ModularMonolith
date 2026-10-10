using Acme.Modules.Good.Contracts.Queries.GetWidget;
using Acme.Modules.Good.Domain.Widgets;
using Acme.SharedKernel;
using Dapper;
using Tsikhanau.Monolith.Application.Queries;
using Tsikhanau.Railway;

namespace Acme.Modules.Good.Features.GetWidget;

internal sealed class GetWidgetQueryHandler(ISqlConnectionFactory<IGoodModule> sqlConnectionFactory)
    : IQueryHandler<GetWidgetQuery, WidgetDto>
{
    private const String Sql = """
        SELECT id AS "Id", label AS "Label", price AS "Price"
        FROM good.widgets
        WHERE id = @WidgetId
        """;

    public async Task<Result<WidgetDto>> Handle(GetWidgetQuery query, CancellationToken cancellationToken)
    {
        await using var connection = await sqlConnectionFactory.OpenConnectionAsync(cancellationToken);
        Optional<WidgetRow> foundWidget = await connection.QuerySingleOrDefaultAsync<WidgetRow>(
            new CommandDefinition(Sql, new { query.WidgetId }, cancellationToken: cancellationToken));

        return foundWidget
            .ToResult(WidgetErrors.NotFound)
            .Bind(ToDto);
    }

    private static Result<WidgetDto> ToDto(WidgetRow widgetRow) =>
        Money
            .Create(widgetRow.Price, nameof(widgetRow.Price))
            .Map(price => new WidgetDto(widgetRow.Id, widgetRow.Label, price));

    private sealed record WidgetRow(Guid Id, String Label, Decimal Price);
}
