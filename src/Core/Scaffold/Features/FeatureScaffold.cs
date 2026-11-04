using Tsikhanau.Monolith.Scaffold.Apps;
using Tsikhanau.Monolith.Scaffold.Cli;
using Tsikhanau.Monolith.Scaffold.Events;
using Tsikhanau.Monolith.Scaffold.Facades;
using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Monolith.Scaffold.Modules;
using Tsikhanau.Monolith.Scaffold.Neighbours;
using Tsikhanau.Monolith.Scaffold.Plans;
using Tsikhanau.Monolith.Scaffold.Sources;
using Tsikhanau.Monolith.Scaffold.Templates;
using Tsikhanau.Monolith.Scaffold.Verifiers;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Features;

internal sealed class FeatureScaffold(FeatureRequest request) : IScaffold
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
            .Bind(module => ReadEvent(app, validRequest, fileSystem)
                .Map(handledEvent => new ScaffoldedFeature(module, validRequest) { Event = handledEvent }));

    private static Result<Optional<EventDeclaration>> ReadEvent(
        AppLayout app,
        FeatureRequest validRequest,
        IFileSystem fileSystem) =>
        validRequest.EventType.Match(
            eventType => new SourceProjectReader(fileSystem)
                .Read(app)
                .Bind(projects => new EventReader(projects).Read(validRequest.ModuleName, eventType))
                .Map(Optional<EventDeclaration>.Some),
            () => Result.Success(Optional<EventDeclaration>.None()));

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
        var files = CreateFiles(feature);

        return Result
            .Collect(CheckFeatureIsNew(feature, files, fileSystem))
            .And(PlanModuleChanges(feature, fileSystem))
            .Bind((note, moduleChanges) => BuildPlan(feature, files, moduleChanges, note));
    }

    private static FeatureFiles CreateFiles(ScaffoldedFeature feature)
    {
        var testTemplate = new FeatureTestTemplate(feature);

        return new FeatureFiles(
            [.. new FeatureTemplate(feature).CreateFiles(), testTemplate.CreateFile()],
            testTemplate.TestMethod);
    }

    private static Result<Optional<String>> CheckFeatureIsNew(
        ScaffoldedFeature feature,
        FeatureFiles files,
        IFileSystem fileSystem) =>
        Result
            .Collect(EnsureNoSuchFeature(feature, files.NewFiles, fileSystem))
            .And(VerifyRoute(feature, fileSystem))
            .And(VerifyInternalCommandName(feature, fileSystem))
            .Map((_, _, note) => note);

    private static Result<ModuleChanges> PlanModuleChanges(ScaffoldedFeature feature, IFileSystem fileSystem) =>
        Result
            .Collect(new FacadeUpdate(feature.Module, fileSystem).Plan(feature.GetRequiredShape))
            .And(new NeighbourReferenceUpdate(feature.Module, fileSystem).Plan(feature.Neighbour))
            .Map((facadeChange, referenceEdits) =>
                new ModuleChanges(facadeChange.NewFiles, [.. facadeChange.FileEdits, .. referenceEdits]));

    private static Result<ScaffoldPlan> BuildPlan(
        ScaffoldedFeature feature,
        FeatureFiles files,
        ModuleChanges moduleChanges,
        Optional<String> note) =>
        CodeLayout
            .EnsureLinesFit([.. moduleChanges.NewFiles, .. files.NewFiles], feature.ChosenNames, feature.ShorterChoice)
            .Map(newFiles =>
                new ScaffoldPlan(newFiles, moduleChanges.FileEdits, [], BuildNextStep(feature, files.TestMethod))
                {
                    Note = note,
                });

    private static Result<Unit> EnsureNoSuchFeature(
        ScaffoldedFeature feature,
        IEnumerable<NewFile> featureFiles,
        IFileSystem fileSystem)
    {
        var existing = GetFeatureFolders(feature, fileSystem)
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

    private static Result<Unit> VerifyRoute(ScaffoldedFeature feature, IFileSystem fileSystem) =>
        feature.Method == EndpointMethod.None
            ? Unit.Value
            : new SourceProjectReader(fileSystem)
                .Read(feature.Module.App, feature.Module.Name)
                .Bind(moduleProjects => new RouteVerifier(moduleProjects).Verify(feature));

    private static Result<Optional<String>> VerifyInternalCommandName(
        ScaffoldedFeature feature,
        IFileSystem fileSystem) =>
        feature.InternalCommandName.Match(
            name => new SourceProjectReader(fileSystem)
                .Read(feature.Module.App, feature.Module.Name)
                .Bind(moduleProjects => new InternalCommandNameVerifier(feature.Module.Name, moduleProjects)
                    .Verify(name)),
            () => Result.Success(Optional<String>.None()));

    private static IEnumerable<String> GetFeatureFolders(ScaffoldedFeature feature, IFileSystem fileSystem) =>
        new[] { feature.FeaturesFolder, feature.PublicQueriesFolder }
            .SelectMany(parentFolder => fileSystem.GetDirectories(parentFolder, "*"))
            .Where(folder => Path
                .GetFileName(folder)
                .Equals(feature.Name, StringComparison.OrdinalIgnoreCase));

    private static String BuildNextStep(ScaffoldedFeature feature, String testMethod) =>
        $"the new test {feature.TestClass}.{testMethod} fails with "
        + $"\"{feature.NotImplementedMessage}\" until the handler does its work; next, run ./gate.sh";
}
