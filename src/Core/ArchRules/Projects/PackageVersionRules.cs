using System.Xml.Linq;
using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;

namespace Tsikhanau.Monolith.ArchRules.Projects;

public static class PackageVersionRules
{
    private const String AspirePackagePrefix = "Aspire.";
    private const String AppHostPattern = "src/Aspire/*.AppHost/*.AppHost.csproj";

    public static RuleEvaluation AspireSdkAndAspirePackagesShareOneVersion(ArchSolution solution)
    {
        IReadOnlyList<(String Name, String Version)> aspirePackages = [.. ReadAspirePackages(solution)];
        IReadOnlyList<String> subject = [.. aspirePackages.Select(package => $"{package.Name} {package.Version}")];

        return RuleEvaluation.Create(subject, () => GetVersionViolations(solution, aspirePackages));
    }

    private static IEnumerable<String> GetVersionViolations(
        ArchSolution solution,
        IReadOnlyList<(String Name, String Version)> aspirePackages)
    {
        var appHostPaths = GetAppHostPaths(solution);

        return appHostPaths is [var appHostPath]
            ? GetVersionMismatches(ReadSdkVersion(appHostPath), aspirePackages)
            : [DescribeAppHostCount(solution, appHostPaths)];
    }

    private static IEnumerable<String> GetVersionMismatches(
        String sdkVersion,
        IEnumerable<(String Name, String Version)> aspirePackages) =>
        aspirePackages
            .Where(package => package.Version != sdkVersion)
            .Select(package => $"{package.Name} {package.Version} differs from the AppHost Sdk {sdkVersion}");

    private static IReadOnlyList<String> GetAppHostPaths(ArchSolution solution) =>
        Directory.Exists(solution.AspireFolder) ? [.. GetAppHostPathsIn(solution.AspireFolder)] : [];

    private static IEnumerable<String> GetAppHostPathsIn(String aspireFolder) =>
        Directory
            .GetDirectories(aspireFolder, "*.AppHost")
            .SelectMany(folder => Directory.GetFiles(folder, "*.AppHost.csproj"))
            .Order(StringComparer.Ordinal);

    private static String DescribeAppHostCount(ArchSolution solution, IReadOnlyList<String> appHostPaths)
    {
        var foundPaths = appHostPaths.Count == 0
            ? "none"
            : String.Join(' ', appHostPaths.Select(solution.ToSolutionPath));

        return $"expected one {AppHostPattern}, found {appHostPaths.Count}: {foundPaths}";
    }

    private static String ReadSdkVersion(String appHostPath)
    {
        var sdk = XDocument
            .Load(appHostPath)
            .Root!
            .Attribute("Sdk")!.Value;

        return sdk[(sdk.IndexOf('/', StringComparison.Ordinal) + 1)..];
    }

    private static IEnumerable<(String Name, String Version)> ReadAspirePackages(ArchSolution solution) =>
        solution.PackageVersionPaths
            .SelectMany(ReadPackageVersions)
            .Where(package => package.Name.StartsWith(AspirePackagePrefix, StringComparison.Ordinal));

    private static IEnumerable<(String Name, String Version)> ReadPackageVersions(String path) =>
        XDocument
            .Load(path)
            .Descendants("PackageVersion")
            .Select(ReadPackageVersion);

    private static (String Name, String Version) ReadPackageVersion(XElement package) =>
        (package.Attribute("Include")!.Value, package.Attribute("Version")!.Value);
}
