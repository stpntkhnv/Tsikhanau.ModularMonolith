using Tsikhanau.Monolith.Testing.Modules;

namespace Tsikhanau.Monolith.UnitTests.Modules;

public sealed class ProductionModulesTests
{
    private const String Placeholder = ProductionModules.Placeholder;

    private const String NoModuleReason = "There are no production modules";

    private const String NoPairReason = "There are fewer than two production modules";

    [Fact]
    public void No_module_gives_a_skipped_placeholder_row_and_a_skipped_placeholder_pair()
    {
        var row = ProductionModules
            .CreateRows([])
            .ShouldHaveSingleItem();

        var pairRow = ProductionModules
            .CreatePairRows([])
            .ShouldHaveSingleItem();

        row.Data.ShouldBe(Placeholder);
        row.Skip.ShouldBe(NoModuleReason);
        pairRow.Data.ShouldBe((Placeholder, Placeholder));
        pairRow.Skip.ShouldBe(NoPairReason);
    }

    [Fact]
    public void One_module_gives_its_row_and_only_a_skipped_placeholder_pair()
    {
        var row = ProductionModules
            .CreateRows(["Orders"])
            .ShouldHaveSingleItem();

        var pairRow = ProductionModules
            .CreatePairRows(["Orders"])
            .ShouldHaveSingleItem();

        row.Data.ShouldBe("Orders");
        row.Skip.ShouldBeNull();
        pairRow.Data.ShouldBe((Placeholder, Placeholder));
        pairRow.Skip.ShouldBe(NoPairReason);
    }

    [Fact]
    public void Two_modules_give_their_rows_and_both_ordered_pairs()
    {
        var rows = ProductionModules.CreateRows(["Orders", "Payments"]);
        var pairRows = ProductionModules.CreatePairRows(["Orders", "Payments"]);
        var names = GetData(rows);
        var pairs = GetData(pairRows);

        names.ShouldBe(["Orders", "Payments"]);
        pairs.ShouldBe([("Orders", "Payments"), ("Payments", "Orders")]);
        rows.ShouldAllBe(row => row.Skip == null);
        pairRows.ShouldAllBe(pairRow => pairRow.Skip == null);
    }

    [Fact]
    public void Missing_modules_folder_gives_no_module()
    {
        var missingFolder = Path.Combine(AppContext.BaseDirectory, "NoModulesFolder");

        var names = ProductionModules.ReadNames(missingFolder);

        names.ShouldBeEmpty();
    }

    private static List<String> GetData(TheoryData<String> rows) => [.. rows.Select(row => row.Data)];

    private static List<(String, String)> GetData(TheoryData<String, String> pairRows) =>
        [.. pairRows.Select(pairRow => pairRow.Data)];
}
