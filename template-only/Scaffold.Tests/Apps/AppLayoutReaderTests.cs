using Tsikhanau.Monolith.Infrastructure.Database;
using Tsikhanau.Monolith.Scaffold.Apps;
using Tsikhanau.Monolith.Scaffold.Tests.Support;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Tests.Apps;

public sealed class AppLayoutReaderTests : IDisposable
{
    private const String HostProjectPattern = "src/Bootstrapper/*.Bootstrapper/*.Bootstrapper.csproj";

    private const String DatabaseNameRule =
        "which cannot name the module roles <database>__<schema>: it must be groups of a-z and 0-9 joined by "
        + "single underscores, and be neither pg nor start with pg_, which PostgreSQL reserves for its own roles";

    private const String ReportsDatabase =
        "var reports = postgres.AddDatabase(\"reports\", databaseName: \"Reports\");\n";

    private const String TwoAppDatabases =
        "AddDatabase(\"database\", databaseName: \"a\");\nAddDatabase(\"database\", databaseName: \"b\");\n";

    private readonly SampleApp _app = SampleApp.Create();

    public void Dispose() => _app.Dispose();

    [Fact]
    public void Layout_holds_the_files_scaffold_edits_and_the_names_of_the_app()
    {
        var layout = _app.ReadLayout();

        layout.ProjectPrefix.ShouldBe("Acme.Shop");
        layout.RootNamespace.ShouldBe("Acme.Shop");
        layout.DatabaseName.ShouldBe("acme_shop");
        layout.Solution.Path.ShouldBe(SampleApp.SolutionPath);
        layout.Solution.Text.ShouldBe(SampleApp.Solution);
        layout.HostProject.Path.ShouldBe(SampleApp.HostProjectPath);
        layout.HostProject.Text.ShouldBe(SampleApp.HostProject);
        layout.Program.Path.ShouldBe(SampleApp.ProgramPath);
        layout.Program.Text.ShouldBe(SampleApp.Program);
        layout.AppSettings.Path.ShouldBe(SampleApp.AppSettingsPath);
        layout.AppSettings.Text.ShouldBe(SampleApp.AppSettings);
    }

    [Fact]
    public void Prefix_comes_from_the_host_project_and_root_namespace_from_the_platform_using()
    {
        _app.Write(SampleApp.ProgramPath, "using AcmeShop.Bootstrapper.Platform;\n");

        var layout = _app.ReadLayout();

        layout.ProjectPrefix.ShouldBe("Acme.Shop");
        layout.RootNamespace.ShouldBe("AcmeShop");
    }

    [Fact]
    public void App_without_a_solution_is_refused()
    {
        _app.Delete(SampleApp.SolutionPath);

        var message = ReadMessage();

        message.ShouldBe("expected one *.slnx in the app root, found 0: none");
    }

    [Fact]
    public void App_with_two_solutions_is_refused()
    {
        _app.Write("Other.slnx", SampleApp.Solution);

        var message = ReadMessage();

        message.ShouldBe("expected one *.slnx in the app root, found 2: Acme.Shop.slnx, Other.slnx");
    }

    [Fact]
    public void App_without_a_host_project_is_refused()
    {
        _app.Delete(SampleApp.HostProjectPath);

        var message = ReadMessage();

        message.ShouldBe($"expected one {HostProjectPattern}, found 0: none");
    }

    [Fact]
    public void App_with_two_host_projects_is_refused()
    {
        _app.Write("src/Bootstrapper/Other.Bootstrapper/Other.Bootstrapper.csproj", SampleApp.HostProject);

        var message = ReadMessage();

        message.ShouldBe(
            $"expected one {HostProjectPattern}, found 2: {SampleApp.HostProjectPath}, "
            + "src/Bootstrapper/Other.Bootstrapper/Other.Bootstrapper.csproj");
    }

    [Fact]
    public void Missing_solution_and_host_project_are_both_named()
    {
        _app.Delete(SampleApp.SolutionPath);
        _app.Delete(SampleApp.HostProjectPath);

        var message = ReadMessage();

        message.ShouldBe(
            "expected one *.slnx in the app root, found 0: none; "
            + $"expected one {HostProjectPattern}, found 0: none");
    }

    [Fact]
    public void Program_without_the_platform_using_is_refused()
    {
        _app.Write(SampleApp.ProgramPath, "using Acme.Shop.Bootstrapper;\n");

        var message = ReadMessage();

        message.ShouldBe(
            $"{SampleApp.ProgramPath} has no 'using <App>.Bootstrapper.Platform;', which names the root namespace");
    }

    [Fact]
    public void Missing_app_settings_are_refused()
    {
        _app.Delete(SampleApp.AppSettingsPath);

        var message = ReadMessage();

        message.ShouldBe($"{SampleApp.AppSettingsPath} does not exist");
    }

    [Fact]
    public void Database_name_is_read_from_the_app_host()
    {
        WriteDatabaseName("shop_eu");

        var layout = _app.ReadLayout();

        layout.DatabaseName.ShouldBe("shop_eu");
    }

    [Fact]
    public void Another_database_in_the_app_host_leaves_the_database_name_of_the_app()
    {
        _app.Write(SampleApp.AppHostPath, SampleApp.AppHost + ReportsDatabase);

        var layout = _app.ReadLayout();

        layout.DatabaseName.ShouldBe(SampleApp.DatabaseName);
    }

    [Fact]
    public void App_without_an_app_host_is_refused()
    {
        _app.Delete(SampleApp.AppHostPath);

        var message = ReadMessage();

        message.ShouldBe("expected one src/Aspire/*.AppHost/AppHost.cs, found 0: none");
    }

    [Theory]
    [InlineData("var builder = DistributedApplication.CreateBuilder(args);\n", 0)]
    [InlineData(ReportsDatabase, 0)]
    [InlineData(TwoAppDatabases, 2)]
    public void App_host_that_does_not_name_the_app_database_once_is_refused(String appHost, Int32 found)
    {
        _app.Write(SampleApp.AppHostPath, appHost);

        var message = ReadMessage();

        message.ShouldBe(
            $"{SampleApp.AppHostPath} must name the app database once, as "
            + $"AddDatabase(\"database\", databaseName: \"<name>\"); found {found}");
    }

    [Theory]
    [InlineData("shop__eu")]
    [InlineData("_shop")]
    [InlineData("shop_")]
    [InlineData("Shop")]
    [InlineData("shop-eu")]
    [InlineData("pg")]
    [InlineData("pg_shop")]
    [InlineData("")]
    public void Database_name_that_breaks_the_rule_is_refused_with_the_rule(String databaseName)
    {
        WriteDatabaseName(databaseName);

        var message = ReadMessage();

        message.ShouldBe($"{SampleApp.AppHostPath} names the database '{databaseName}', {DatabaseNameRule}");
    }

    [Fact]
    public void Database_name_of_54_characters_is_accepted()
    {
        var databaseName = new String('d', 54);
        WriteDatabaseName(databaseName);

        var layout = _app.ReadLayout();

        layout.DatabaseName.ShouldBe(databaseName);
    }

    [Fact]
    public void Database_name_whose_group_would_pass_63_characters_is_refused()
    {
        var databaseName = new String('d', 55);
        WriteDatabaseName(databaseName);

        var message = ReadMessage();

        message.ShouldBe(
            $"{SampleApp.AppHostPath} names the database '{databaseName}', which makes the group role "
            + $"{databaseName}__modules 64 characters long, more than the 63 PostgreSQL allows in a role name: "
            + "shorten it to at most 54 characters");
    }

    [Theory]
    [InlineData("acme_shop")]
    [InlineData("1shop")]
    [InlineData("pgx")]
    [InlineData("p_g")]
    [InlineData("shop__eu")]
    [InlineData("shop_")]
    [InlineData("Shop")]
    [InlineData("pg")]
    [InlineData("pg_shop")]
    [InlineData("dddddddddddddddddddddddddddddddddddddddddddddddddddddd")]
    [InlineData("ddddddddddddddddddddddddddddddddddddddddddddddddddddddd")]
    public void Scaffold_accepts_exactly_the_database_names_the_core_accepts(String databaseName)
    {
        WriteDatabaseName(databaseName);

        var layout = new AppLayoutReader(_app.FileSystem).Read();

        layout.IsSuccess.ShouldBe(AcceptsInCore(databaseName), ReadMessage());
    }

    private static Boolean AcceptsInCore(String databaseName)
    {
        try
        {
            _ = new ModuleLoginNames(databaseName);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private void WriteDatabaseName(String databaseName) =>
        _app.Write(
            SampleApp.AppHostPath,
            SampleApp.AppHost.Replace(
                $"databaseName: \"{SampleApp.DatabaseName}\"",
                $"databaseName: \"{databaseName}\"",
                StringComparison.Ordinal));

    private String ReadMessage() =>
        new AppLayoutReader(_app.FileSystem)
            .Read()
            .Match(_ => "success", error => error.Message);
}
