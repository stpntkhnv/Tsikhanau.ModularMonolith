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
        new("Acme.Modules.Good.Contracts", []),
        new("Acme.Modules.Peer.Contracts", []),
        new("Acme.Modules.Bad.Contracts", []),
        new("Acme.Modules.Good", ["Acme.Modules.Good.Contracts", "Acme.Modules.Peer.Contracts"]),
        new("Acme.Modules.Peer", ["Acme.Modules.Peer.Contracts"]),
        new("Acme.Modules.Bad", ["Acme.Modules.Bad.Contracts", "Acme.Modules.Peer.Contracts", "Acme.Modules.Peer"]),
        new("Acme.Bootstrapper", ["Acme.Modules.Good", "Acme.Modules.Peer"]),
    ];

    private static readonly Lazy<IReadOnlyDictionary<String, Assembly>> Compiled = new(CompileAll);

    public static Assembly Get(String assemblyName) => Compiled.Value[assemblyName];

    private static IReadOnlyDictionary<String, Assembly> CompileAll()
    {
        var folder = Directory.CreateTempSubdirectory("arch-fixtures-").FullName;
        AppDomain.CurrentDomain.ProcessExit += (_, _) => DeleteFolder(folder);
        var images = new Dictionary<String, MetadataReference>(StringComparer.Ordinal);

        foreach (var project in Projects)
        {
            var image = FixtureCompiler.Compile(
                project.Name,
                ReadSyntaxTrees(project),
                [.. FixtureCompiler.RuntimeReferences, .. project.References.Select(name => images[name])]);

            File.WriteAllBytes(BuildPath(folder, project.Name), image);
            images[project.Name] = MetadataReference.CreateFromImage(image);
        }

        return Projects.ToDictionary(
            project => project.Name,
            project => Assembly.LoadFrom(BuildPath(folder, project.Name)),
            StringComparer.Ordinal);
    }

    private static IEnumerable<SyntaxTree> ReadSyntaxTrees(FixtureProject project)
    {
        var resourceNames = typeof(FixtureAssemblies).Assembly
            .GetManifestResourceNames()
            .Where(resourceName => IsSourceOf(project, resourceName));

        return [CSharpSyntaxTree.ParseText(ImplicitUsings), .. resourceNames.Select(ReadSyntaxTree)];
    }

    private static Boolean IsSourceOf(FixtureProject project, String resourceName) =>
        ToPath(resourceName)
            .StartsWith($"{ResourcePrefix}{project.Name}/", StringComparison.Ordinal);

    private static SyntaxTree ReadSyntaxTree(String resourceName)
    {
        using var stream = typeof(FixtureAssemblies).Assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return CSharpSyntaxTree.ParseText(reader.ReadToEnd(), path: ToPath(resourceName));
    }

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
}
