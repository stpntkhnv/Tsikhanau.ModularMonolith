using Tsikhanau.Monolith.Scaffold.Modules;
using Tsikhanau.Monolith.Scaffold.Tests.Support;

namespace Tsikhanau.Monolith.Scaffold.Tests.Modules;

public sealed class FacadeTemplateTests
{
    private const String LongName = "InventoryReservationAdjustmentApprovals";

    private const String MaxName = "Abbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private static readonly FacadeShape WithCommands = FacadeShape.Fresh.WithCommands();

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

    [Theory]
    [InlineData("Notes")]
    [InlineData(LongName)]
    [InlineData(MaxName)]
    public void Every_line_of_the_facade_with_commands_fits_in_120_characters(String name)
    {
        var facadeTemplate = new FacadeTemplate(SampleModules.Create(name));
        IEnumerable<String> texts =
        [
            facadeTemplate.RenderFacadeContract(WithCommands),
            facadeTemplate.RenderFacadeClass(WithCommands),
            facadeTemplate.CommandMarkerFile.Content,
        ];

        var longLines = texts
            .SelectMany(text => text.Split('\n'))
            .Where(line => line.Length > 120);

        longLines.ShouldBeEmpty();
    }
}
