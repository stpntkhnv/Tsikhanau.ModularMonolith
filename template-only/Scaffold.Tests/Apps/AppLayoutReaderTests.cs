using Tsikhanau.Monolith.Scaffold.Apps;
using Tsikhanau.Monolith.Scaffold.Tests.Support;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Tests.Apps;

public sealed class AppLayoutReaderTests : IDisposable
{
    private const String HostProjectPattern = "src/Bootstrapper/*.Bootstrapper/*.Bootstrapper.csproj";

    private readonly SampleApp _app = SampleApp.Create();

    public void Dispose() => _app.Dispose();

    [Fact]
    public void Layout_holds_the_files_scaffold_edits_and_the_names_of_the_app()
    {
        var layout = _app.ReadLayout();

        layout.ProjectPrefix.ShouldBe("Acme.Shop");
        layout.RootNamespace.ShouldBe("Acme.Shop");
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

    private String ReadMessage() =>
        new AppLayoutReader(_app.FileSystem)
            .Read()
            .Match(_ => "success", error => error.Message);
}
