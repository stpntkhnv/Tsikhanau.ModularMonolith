using System.Text;
using Tsikhanau.Monolith.Scaffold.Apps;
using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Monolith.Scaffold.Modules;

namespace Tsikhanau.Monolith.Scaffold.Tests.Support;

internal static class SampleModules
{
    public static ScaffoldedModule Create(String name, String appName = SampleApp.Name) =>
        new(
            CreateLayout(appName),
            ModuleName
                .Parse(name, SampleApp.LoginPrefix)
                .Value);

    public static TextFile CreateFile(String path, String text) => new(path, Encoding.UTF8.GetBytes(text));

    public static String RenameApp(String text, String appName) =>
        text.Replace(SampleApp.Name, appName, StringComparison.Ordinal);

    private static AppLayout CreateLayout(String appName) =>
        new(
            appName,
            appName,
            SampleApp.LoginPrefix,
            CreateFile(RenameApp(SampleApp.SolutionPath, appName), RenameApp(SampleApp.Solution, appName)),
            CreateFile(RenameApp(SampleApp.HostProjectPath, appName), RenameApp(SampleApp.HostProject, appName)),
            CreateFile(RenameApp(SampleApp.ProgramPath, appName), RenameApp(SampleApp.Program, appName)),
            CreateFile(RenameApp(SampleApp.AppSettingsPath, appName), RenameApp(SampleApp.AppSettings, appName)));
}
