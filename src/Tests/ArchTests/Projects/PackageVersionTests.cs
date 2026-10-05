using System.Xml.Linq;
using ModularMonolith.Testing.Modules;

namespace ModularMonolith.ArchTests.Projects;

public sealed class PackageVersionTests
{
    private const String AspirePackagePrefix = "Aspire.";

    [Fact]
    public void Aspire_sdk_and_aspire_packages_share_one_version()
    {
        var sdkVersion = ReadAppHostSdkVersion();
        var aspirePackageVersions = ReadCentralPackageVersions()
            .Where(package => package.Name.StartsWith(AspirePackagePrefix, StringComparison.Ordinal))
            .Select(package => package.Version)
            .ToList();

        aspirePackageVersions.ShouldNotBeEmpty();
        aspirePackageVersions.ShouldAllBe(
            version => version == sdkVersion,
            "Bump the Aspire SDK in the AppHost Sdk attribute together with the Aspire packages");
    }

    private static String ReadAppHostSdkVersion()
    {
        var appHostPath = Path.Combine(
            SolutionPaths.Root,
            "src",
            "Aspire",
            "ModularMonolith.AppHost",
            "ModularMonolith.AppHost.csproj");

        var sdk = XDocument
            .Load(appHostPath)
            .Root!
            .Attribute("Sdk")!.Value;

        return sdk[(sdk.IndexOf('/', StringComparison.Ordinal) + 1)..];
    }

    private static IEnumerable<(String Name, String Version)> ReadCentralPackageVersions() =>
        XDocument
            .Load(Path.Combine(SolutionPaths.Root, "Directory.Packages.props"))
            .Descendants("PackageVersion")
            .Select(package => (package.Attribute("Include")!.Value, package.Attribute("Version")!.Value));
}
