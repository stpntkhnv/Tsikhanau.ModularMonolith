using Tsikhanau.Monolith.Testing.Modules;

namespace Tsikhanau.Monolith.UnitTests.Modules;

public sealed class ProductionModulesTests
{
    private const String Placeholder = ProductionModules.Placeholder;

    [Fact]
    public void No_module_gives_the_placeholder_row_and_the_placeholder_pair()
    {
        var rows = GetData(ProductionModules.CreateRows([]));
        var pairRows = GetData(ProductionModules.CreatePairRows([]));

        rows.ShouldBe([Placeholder]);
        pairRows.ShouldBe([(Placeholder, Placeholder)]);
    }

    [Fact]
    public void One_module_gives_its_row_and_only_the_placeholder_pair()
    {
        var rows = GetData(ProductionModules.CreateRows(["Orders"]));
        var pairRows = GetData(ProductionModules.CreatePairRows(["Orders"]));

        rows.ShouldBe(["Orders"]);
        pairRows.ShouldBe([(Placeholder, Placeholder)]);
    }

    [Fact]
    public void Two_modules_give_their_rows_and_both_ordered_pairs()
    {
        var rows = GetData(ProductionModules.CreateRows(["Orders", "Payments"]));
        var pairRows = GetData(ProductionModules.CreatePairRows(["Orders", "Payments"]));

        rows.ShouldBe(["Orders", "Payments"]);
        pairRows.ShouldBe([("Orders", "Payments"), ("Payments", "Orders")]);
    }

    private static List<String> GetData(TheoryData<String> rows) => [.. rows.Select(row => row.Data)];

    private static List<(String, String)> GetData(TheoryData<String, String> pairRows) =>
        [.. pairRows.Select(pairRow => pairRow.Data)];
}
