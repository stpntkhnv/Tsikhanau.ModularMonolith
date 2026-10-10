using Tsikhanau.Monolith.Testing.Modules;

namespace Tsikhanau.Monolith.ArchRules.Tests.Template;

public sealed class TemplateSourcesTests
{
    private static readonly String CoreFilesPath = Path.Combine(SolutionPaths.Root, "src", "Core", "core-files.txt");

    private static readonly String[] NeverShipped =
    [
        "template-only/**/*",
        ".template.config/**/*",
        ".github/workflows/template.yml",
        "docs/**/*",
        ".superpowers/**/*",
        ".worktrees/**/*",
        ".claude/**/*",
        "AGENTS.md",
        "CLAUDE.md",
        "STYLE.md",
        ".git/**/*",
        "**/[Bb]in/**",
        "**/[Oo]bj/**",
        ".nuget/**/*",
    ];

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Copy_only_holds_the_core_files_and_core_lock()
    {
        var templateSource = await TemplateSource.ReadAsync(CancellationToken);
        var coreEntries = await File.ReadAllLinesAsync(CoreFilesPath, CancellationToken);

        var coreGlobs = coreEntries
            .Select(BuildCoreGlob)
            .Append("core.lock");

        templateSource.CopyOnly.ShouldBe(coreGlobs, ignoreOrder: true);
    }

    [Fact]
    public async Task Every_tracked_path_is_shipped_or_excluded_on_purpose()
    {
        var templateSource = await TemplateSource.ReadAsync(CancellationToken);
        var trackedPaths = await RepositoryCommands.RunAsync("git", ["ls-files"], []);

        var unclassifiedPaths = trackedPaths.Where(path => !templateSource.IsClassified(path));

        trackedPaths.ShouldNotBeEmpty();
        unclassifiedPaths.ShouldBeEmpty();
    }

    [Fact]
    public async Task Exclude_holds_what_an_app_never_gets()
    {
        var templateSource = await TemplateSource.ReadAsync(CancellationToken);

        templateSource.Exclude.ShouldBe(NeverShipped, ignoreOrder: true);
    }

    private static String BuildCoreGlob(String coreEntry) => coreEntry.EndsWith('/') ? $"{coreEntry}**/*" : coreEntry;
}
