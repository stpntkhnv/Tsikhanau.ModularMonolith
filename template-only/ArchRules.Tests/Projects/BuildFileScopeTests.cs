using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Projects;
using Tsikhanau.Monolith.ArchRules.Rules;

namespace Tsikhanau.Monolith.ArchRules.Tests.Projects;

public sealed class BuildFileScopeTests : IDisposable
{
    private const String AppProject = "src/App/App.csproj";

    private readonly DirectoryInfo _scratchFolder = Directory.CreateTempSubdirectory("build-file-scope-");

    public BuildFileScopeTests()
    {
        AddFile(".editorconfig");
        AddFile("Directory.Build.props");
        AddProject(AppProject);
        AddSolution(AppProject);
    }

    private String Root => Path.Combine(_scratchFolder.FullName, "repository");

    [Fact]
    public void Config_file_in_a_folder_that_is_neither_a_project_folder_nor_above_one_is_not_checked()
    {
        AddFile("web/.editorconfig");
        AddFile("node_modules/package/.editorconfig");

        var evaluation = Evaluate();

        evaluation.Subject.ShouldBe([".editorconfig", "Directory.Build.props"]);
        evaluation.Violations.ShouldBeEmpty();
    }

    [Fact]
    public void Config_file_in_a_folder_above_a_project_is_reported()
    {
        AddFile("src/.editorconfig");

        var evaluation = Evaluate();

        evaluation.Violations.ShouldBe(["src/.editorconfig"]);
    }

    [Fact]
    public void Config_file_inside_a_project_folder_is_reported_but_not_in_its_build_output_hidden_or_package_folders()
    {
        AddFile("src/App/Features/Feature.cs");
        AddFile("src/App/Features/.editorconfig");
        AddFile("src/App/bin/Debug/Directory.Build.props");
        AddFile("src/App/obj/Directory.Build.props");
        AddFile("src/App/.vs/Directory.Build.props");
        AddFile("src/App/node_modules/package/Directory.Build.props");

        var evaluation = Evaluate();

        evaluation.Violations.ShouldBe(["src/App/Features/.editorconfig"]);
    }

    [Fact]
    public void Only_build_files_count_in_a_project_subfolder_without_csharp_sources()
    {
        AddFile("src/App/ClientApp/main.ts");
        AddFile("src/App/ClientApp/.editorconfig");
        AddFile("src/App/ClientApp/Directory.Build.props");

        var evaluation = Evaluate();

        evaluation.Violations.ShouldBe(["src/App/ClientApp/Directory.Build.props"]);
    }

    [Fact]
    public void Editor_config_in_a_project_subfolder_next_to_csharp_sources_is_reported()
    {
        AddFile("src/App/ClientApp/main.ts");
        AddFile("src/App/ClientApp/Interop.cs");
        AddFile("src/App/ClientApp/.editorconfig");

        var evaluation = Evaluate();

        evaluation.Violations.ShouldBe(["src/App/ClientApp/.editorconfig"]);
    }

    [Fact]
    public void Editor_config_in_the_folder_of_a_project_without_csharp_sources_is_reported()
    {
        AddFile("src/App/.editorconfig");

        var evaluation = Evaluate();

        evaluation.Violations.ShouldBe(["src/App/.editorconfig"]);
    }

    [Fact]
    public void Project_in_a_linked_folder_is_checked()
    {
        AddProject("../elsewhere/Tools/Tool/Tool.csproj");
        AddFile("../elsewhere/Tools/.editorconfig");
        AddFile("../elsewhere/Tools/Tool/.globalconfig");
        AddFolderLink("src/Tools", "../elsewhere/Tools");
        AddSolution(AppProject, "src/Tools/Tool/Tool.csproj");

        var evaluation = Evaluate();

        evaluation.Violations.ShouldBe(["src/Tools/.editorconfig", "src/Tools/Tool/.globalconfig"]);
    }

    [Fact]
    public void Project_in_a_nested_repository_is_checked()
    {
        AddFile("src/Vendor/.git");
        AddFile("src/Vendor/.editorconfig");
        AddProject("src/Vendor/Tool/Tool.csproj");
        AddSolution(AppProject, "src/Vendor/Tool/Tool.csproj");

        var evaluation = Evaluate();

        evaluation.Violations.ShouldBe(["src/Vendor/.editorconfig"]);
    }

    [Fact]
    public void Project_built_through_a_reference_of_a_solution_project_is_checked()
    {
        AddProject(AppProject, "../../libs/Library/Library.csproj");
        AddProject("libs/Library/Library.csproj");
        AddFile("libs/Directory.Build.props");

        var evaluation = Evaluate();

        evaluation.Violations.ShouldBe(["libs/Directory.Build.props"]);
    }

    [Fact]
    public void Project_of_the_template_solution_is_checked()
    {
        AddProject("template-only/Tool/Tool.csproj");
        AddFile("template-only/Tool/Directory.Build.props");
        AddFile("template-only/Template.slnx", BuildSolution("Tool/Tool.csproj"));

        var evaluation = Evaluate();

        evaluation.Violations.ShouldBe(["template-only/Tool/Directory.Build.props"]);
    }

    public void Dispose() => _scratchFolder.Delete(recursive: true);

    private static String BuildSolution(params IEnumerable<String> projectPaths) =>
        String.Join(
            '\n',
            [
                "<Solution>",
                .. projectPaths.Select(path => $"  <Project Path=\"{path}\" />"),
                "</Solution>",
            ]);

    private static String BuildProject(IEnumerable<String> referencePaths) =>
        String.Join(
            '\n',
            [
                "<Project Sdk=\"Microsoft.NET.Sdk\">",
                "  <ItemGroup>",
                .. referencePaths.Select(path => $"    <ProjectReference Include=\"{path}\" />"),
                "  </ItemGroup>",
                "</Project>",
            ]);

    private RuleEvaluation Evaluate() =>
        BuildFileRules.OnlyTheCoreHasBuildAndAnalyzerConfigFiles(new ArchSolution(Root, "Scratch"));

    private void AddSolution(params IEnumerable<String> projectPaths) =>
        AddFile("Scratch.slnx", BuildSolution(projectPaths));

    private void AddProject(String path, params IEnumerable<String> referencePaths) =>
        AddFile(path, BuildProject(referencePaths));

    private void AddFile(String path, String text = "")
    {
        var fullPath = Path.GetFullPath(path, Root);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, text);
    }

    private void AddFolderLink(String path, String target) =>
        Directory.CreateSymbolicLink(Path.GetFullPath(path, Root), Path.GetFullPath(target, Root));
}
