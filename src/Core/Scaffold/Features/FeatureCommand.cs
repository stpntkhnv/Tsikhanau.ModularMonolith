using Tsikhanau.Monolith.Scaffold.Apps;
using Tsikhanau.Monolith.Scaffold.Cli;
using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Monolith.Scaffold.Modules;
using Tsikhanau.Monolith.Scaffold.Transactions;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Features;

internal sealed class FeatureCommand(FeatureRequest request) : IScaffoldCommand
{
    private const String TestProjectPattern = "*.IntegrationTests.csproj";

    public Result<ScaffoldPlan> Plan(AppLayout app, IFileSystem fileSystem) =>
        request
            .Validate()
            .Bind(validRequest => ReadFeature(app, validRequest, fileSystem))
            .Bind(feature => EnsureTestProject(feature, fileSystem))
            .Bind(feature => PlanFeature(feature, fileSystem));

    private static Result<ScaffoldedFeature> ReadFeature(
        AppLayout app,
        FeatureRequest validRequest,
        IFileSystem fileSystem) =>
        new ModuleReader(fileSystem)
            .Read(app, validRequest.ModuleName)
            .Map(module => new ScaffoldedFeature(module, validRequest));

    private static Result<ScaffoldedFeature> EnsureTestProject(ScaffoldedFeature feature, IFileSystem fileSystem) =>
        fileSystem
            .GetFiles(ScaffoldedFeature.TestProjectFolder, TestProjectPattern)
            .Count == 1
            ? feature
            : Error.Failure(
                "scaffold.app-layout",
                $"{ScaffoldedFeature.TestProjectFolder} must hold one {TestProjectPattern}, "
                + "where scaffold puts the feature's test");

    private static Result<ScaffoldPlan> PlanFeature(ScaffoldedFeature feature, IFileSystem fileSystem)
    {
        var testTemplate = new FeatureTestTemplate(feature);
        var nextStep = BuildNextStep(feature, testTemplate);
        IReadOnlyList<NewFile> featureFiles =
        [
            .. new FeatureTemplate(feature).CreateFiles(),
            testTemplate.CreateFile(),
        ];

        return Result
            .Collect(EnsureNoSuchFeature(feature, featureFiles, fileSystem))
            .And(new FacadeUpdate(feature.Module, fileSystem).Plan(shape => shape.WithCommands()))
            .Bind((_, facadeChange) => CombinePlan(feature, facadeChange, featureFiles, nextStep));
    }

    private static Result<ScaffoldPlan> CombinePlan(
        ScaffoldedFeature feature,
        FacadeChange facadeChange,
        IReadOnlyList<NewFile> featureFiles,
        String nextStep) =>
        CodeLayout
            .EnsureLinesFit([.. facadeChange.NewFiles, .. featureFiles], $"feature name '{feature.Name}'")
            .Map(newFiles => new ScaffoldPlan(newFiles, facadeChange.FileEdits, [], nextStep));

    private static Result<Unit> EnsureNoSuchFeature(
        ScaffoldedFeature feature,
        IEnumerable<NewFile> featureFiles,
        IFileSystem fileSystem)
    {
        var existing = FindFeatureFolders(feature, fileSystem)
            .Concat(featureFiles
                .Select(newFile => newFile.Path)
                .Where(fileSystem.FileExists))
            .ToList();

        return existing.Count == 0
            ? Unit.Value
            : Error.Failure(
                "scaffold.exists",
                $"feature {feature.Name} of {feature.Module.Name} already exists: {String.Join(", ", existing)}");
    }

    private static IEnumerable<String> FindFeatureFolders(ScaffoldedFeature feature, IFileSystem fileSystem) =>
        fileSystem
            .GetDirectories(feature.FeaturesFolder, "*")
            .Where(folder => Path
                .GetFileName(folder)
                .Equals(feature.Name, StringComparison.OrdinalIgnoreCase));

    private static String BuildNextStep(ScaffoldedFeature feature, FeatureTestTemplate testTemplate) =>
        $"the new test {feature.TestClass}.{testTemplate.TestMethod} fails with \"{feature.NotImplementedMessage}\" "
        + "until the handler does its work; next, run ./gate.sh";
}
