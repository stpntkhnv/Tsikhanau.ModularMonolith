using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Tsikhanau.Monolith.ArchRules.Tests.Fixtures;

internal static class FixtureAssemblies
{
    private const String ResourcePrefix = "Fixtures/";

    private const String ImplicitUsings = """
        global using System;
        global using System.Collections.Generic;
        global using System.Linq;
        global using System.Threading;
        global using System.Threading.Tasks;
        """;

    private static readonly IReadOnlyList<FixtureProject> Projects =
    [
        new("Acme.SharedKernel", []),
        new("Acme.Modules.Good.Contracts", ["Acme.SharedKernel"]),
        new("Acme.Modules.Peer.Contracts", []),
        new("Acme.Modules.Bad.Contracts", []),
        new("Acme.Modules.Good", ["Acme.Modules.Good.Contracts", "Acme.Modules.Peer.Contracts", "Acme.SharedKernel"]),
        new("Acme.Modules.Peer", ["Acme.Modules.Peer.Contracts"]),
        new("Acme.Modules.Bad", ["Acme.Modules.Bad.Contracts", "Acme.Modules.Peer.Contracts", "Acme.Modules.Peer"]),
        new("Acme.Bootstrapper", ["Acme.Modules.Good", "Acme.Modules.Peer"]),
        new("Tsikhanau.Monolith.Shared.Fixture", ["Acme.SharedKernel"]),
    ];

    private static readonly Lazy<IReadOnlyDictionary<String, Assembly>> Compiled = new(CompileAll);

    public static Assembly Get(String assemblyName) => Compiled.Value[assemblyName];

    public static Assembly CompileWithout(String assemblyName, String removedText)
    {
        var project = Projects.Single(project => project.Name == assemblyName);
        IReadOnlyList<FixtureSource> sources = [.. ReadSources(project)];

        sources
            .Count(source => source.Contains(removedText))
            .ShouldBe(1, $"exactly one source of {assemblyName} holds the removed text");

        var image = FixtureCompiler.Compile(
            $"{assemblyName}.Edited",
            Parse(sources.Select(source => source.Without(removedText))),
            [.. FixtureCompiler.RuntimeReferences, .. project.References.Select(CreateCompiledReference)]);

        return Assembly.Load(image);
    }

    private static IReadOnlyDictionary<String, Assembly> CompileAll()
    {
        var folder = Directory.CreateTempSubdirectory("arch-fixtures-").FullName;
        AppDomain.CurrentDomain.ProcessExit += (_, _) => DeleteFolder(folder);
        var images = new Dictionary<String, MetadataReference>(StringComparer.Ordinal);

        foreach (var project in Projects)
        {
            var image = FixtureCompiler.Compile(
                project.Name,
                Parse(ReadSources(project)),
                [.. FixtureCompiler.RuntimeReferences, .. project.References.Select(name => images[name])]);

            File.WriteAllBytes(BuildPath(folder, project.Name), image);
            images[project.Name] = MetadataReference.CreateFromImage(image);
        }

        return Projects.ToDictionary(
            project => project.Name,
            project => Assembly.LoadFrom(BuildPath(folder, project.Name)),
            StringComparer.Ordinal);
    }

    private static IEnumerable<FixtureSource> ReadSources(FixtureProject project) =>
        typeof(FixtureAssemblies).Assembly
            .GetManifestResourceNames()
            .Where(resourceName => IsSourceOf(project, resourceName))
            .Select(ReadSource);

    private static IEnumerable<SyntaxTree> Parse(IEnumerable<FixtureSource> sources) =>
    [
        CSharpSyntaxTree.ParseText(ImplicitUsings),
        .. sources.Select(source => CSharpSyntaxTree.ParseText(source.Text, path: source.Path)),
    ];

    private static Boolean IsSourceOf(FixtureProject project, String resourceName) =>
        ToPath(resourceName)
            .StartsWith($"{ResourcePrefix}{project.Name}/", StringComparison.Ordinal);

    private static FixtureSource ReadSource(String resourceName)
    {
        using var stream = typeof(FixtureAssemblies).Assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return new FixtureSource(ToPath(resourceName), reader.ReadToEnd());
    }

    private static MetadataReference CreateCompiledReference(String assemblyName) =>
        MetadataReference.CreateFromFile(Get(assemblyName).Location);

    private static String ToPath(String resourceName) => resourceName.Replace('\\', '/');

    private static String BuildPath(String folder, String assemblyName) => Path.Combine(folder, $"{assemblyName}.dll");

    private static void DeleteFolder(String folder)
    {
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record FixtureProject(String Name, IReadOnlyList<String> References);

    private sealed record FixtureSource(String Path, String Text)
    {
        public Boolean Contains(String text) => Text.Contains(text, StringComparison.Ordinal);

        public FixtureSource Without(String text) =>
            this with { Text = Text.Replace(text, String.Empty, StringComparison.Ordinal) };
    }
}
