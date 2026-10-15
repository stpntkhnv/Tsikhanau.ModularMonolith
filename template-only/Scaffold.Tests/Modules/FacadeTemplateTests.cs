using Tsikhanau.Monolith.Scaffold.Modules;
using Tsikhanau.Monolith.Scaffold.Tests.Support;
using Tsikhanau.Monolith.Scaffold.Transactions;

namespace Tsikhanau.Monolith.Scaffold.Tests.Modules;

public sealed class FacadeTemplateTests
{
    private const String LongName = "InventoryReservationAdjustmentApprovals";

    private const String MaxName = "Abbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private static readonly FacadeShape WithCommands = FacadeShape.Fresh.WithCommands();

    private static readonly FacadeShape WithQueries = FacadeShape.Fresh.WithQueries();

    private static readonly FacadeShape WithCommandsAndQueries = WithCommands.WithQueries();

    private static readonly FacadeShape[] Shapes =
        [FacadeShape.Fresh, WithCommands, WithQueries, WithCommandsAndQueries];

    [Fact]
    public void Facade_of_a_long_module_with_commands_wraps_its_declaration_and_its_method()
    {
        var facadeTemplate = new FacadeTemplate(SampleModules.Create(LongName));

        var facadeClass = facadeTemplate.RenderFacadeClass(WithCommands);

        facadeClass.ShouldEndWith(
            """
            internal sealed class InventoryReservationAdjustmentApprovalsModuleFacade(IServiceScopeFactory serviceScopeFactory)
                : ModuleFacade<IInventoryReservationAdjustmentApprovalsModule>(serviceScopeFactory),
                    IInventoryReservationAdjustmentApprovalsModule
            {
                public Task<Result<TResult>> ExecuteCommandAsync<TResult>(
                    IInventoryReservationAdjustmentApprovalsCommand<TResult> command,
                    CancellationToken cancellationToken)
                    where TResult : notnull =>
                    SendAsync(command, cancellationToken);
            }

            """,
            Case.Sensitive);
    }

    [Fact]
    public void Facade_contract_of_a_short_module_keeps_its_method_on_one_line_when_it_fits()
    {
        var facadeTemplate = new FacadeTemplate(SampleModules.Create("Ab"));

        var facadeContract = facadeTemplate.RenderFacadeContract(WithCommands);

        facadeContract.ShouldEndWith(
            """
            internal interface IAbModule : IModuleFacade
            {
                Task<Result<TResult>> ExecuteCommandAsync<TResult>(IAbCommand<TResult> command, CancellationToken cancellationToken)
                    where TResult : notnull;
            }

            """,
            Case.Sensitive);
    }

    [Fact]
    public void Facade_of_a_long_module_with_commands_and_queries_wraps_its_bases_and_both_methods()
    {
        var facadeTemplate = new FacadeTemplate(SampleModules.Create(LongName));

        var facadeContract = facadeTemplate.RenderFacadeContract(WithCommandsAndQueries);
        var facadeClass = facadeTemplate.RenderFacadeClass(WithCommandsAndQueries);

        facadeContract.ShouldEndWith(
            """
            internal interface IInventoryReservationAdjustmentApprovalsModule
                : IInventoryReservationAdjustmentApprovalsQueryFacade, IModuleFacade
            {
                Task<Result<TResult>> ExecuteCommandAsync<TResult>(
                    IInventoryReservationAdjustmentApprovalsCommand<TResult> command,
                    CancellationToken cancellationToken)
                    where TResult : notnull;
            }

            """,
            Case.Sensitive);

        facadeClass.ShouldEndWith(
            """
            {
                public Task<Result<TResult>> ExecuteCommandAsync<TResult>(
                    IInventoryReservationAdjustmentApprovalsCommand<TResult> command,
                    CancellationToken cancellationToken)
                    where TResult : notnull =>
                    SendAsync(command, cancellationToken);

                public Task<Result<TResult>> ExecuteQueryAsync<TResult>(
                    IInventoryReservationAdjustmentApprovalsQuery<TResult> query,
                    CancellationToken cancellationToken)
                    where TResult : notnull =>
                    QueryAsync(query, cancellationToken);
            }

            """,
            Case.Sensitive);
    }

    [Fact]
    public void Query_facade_of_a_long_module_wraps_its_method()
    {
        var facadeTemplate = new FacadeTemplate(SampleModules.Create(LongName));

        var queryFacade = facadeTemplate.QueryFacadeFile;

        queryFacade.ShouldBe(
            new NewFile(
                "src/Modules/InventoryReservationAdjustmentApprovals/"
                + "Acme.Shop.Modules.InventoryReservationAdjustmentApprovals.Contracts/"
                + "IInventoryReservationAdjustmentApprovalsQueryFacade.cs",
                """
                using Tsikhanau.Monolith.Contracts.Modules;
                using Tsikhanau.Railway;

                namespace Acme.Shop.Modules.InventoryReservationAdjustmentApprovals.Contracts;

                public interface IInventoryReservationAdjustmentApprovalsQueryFacade : IModuleQueryFacade
                {
                    Task<Result<TResult>> ExecuteQueryAsync<TResult>(
                        IInventoryReservationAdjustmentApprovalsQuery<TResult> query,
                        CancellationToken cancellationToken)
                        where TResult : notnull;
                }

                """));
    }

    [Theory]
    [InlineData("Notes")]
    [InlineData(LongName)]
    [InlineData(MaxName)]
    public void Every_line_of_every_facade_shape_and_marker_fits_in_120_characters(String name)
    {
        var facadeTemplate = new FacadeTemplate(SampleModules.Create(name));
        IEnumerable<String> texts =
        [
            .. Shapes.Select(facadeTemplate.RenderFacadeContract),
            .. Shapes.Select(facadeTemplate.RenderFacadeClass),
            facadeTemplate.CommandMarkerFile.Content,
            facadeTemplate.QueryMarkerFile.Content,
            facadeTemplate.QueryFacadeFile.Content,
        ];

        var longLines = texts
            .SelectMany(text => text.Split('\n'))
            .Where(line => line.Length > 120);

        longLines.ShouldBeEmpty();
    }
}
