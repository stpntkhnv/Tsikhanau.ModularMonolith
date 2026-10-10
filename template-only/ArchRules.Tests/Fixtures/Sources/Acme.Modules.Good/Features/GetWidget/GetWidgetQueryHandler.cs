using Acme.Modules.Good.Contracts.Queries.GetWidget;
using Acme.Modules.Good.Domain.Widgets;
using Dapper;
using Tsikhanau.Monolith.Application.Queries;
using Tsikhanau.Railway;

namespace Acme.Modules.Good.Features.GetWidget;

internal sealed class GetWidgetQueryHandler(ISqlConnectionFactory<IGoodModule> sqlConnectionFactory)
    : IQueryHandler<GetWidgetQuery, WidgetDto>
{
    private const String Sql = """
        SELECT id AS "Id", label AS "Label"
        FROM good.widgets
        WHERE id = @WidgetId
        """;

    public async Task<Result<WidgetDto>> Handle(GetWidgetQuery query, CancellationToken cancellationToken)
    {
        await using var connection = await sqlConnectionFactory.OpenConnectionAsync(cancellationToken);
        Optional<WidgetDto> foundWidget = await connection.QuerySingleOrDefaultAsync<WidgetDto>(
            new CommandDefinition(Sql, new { query.WidgetId }, cancellationToken: cancellationToken));

        return foundWidget.ToResult(WidgetErrors.NotFound);
    }
}
