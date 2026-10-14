using Tsikhanau.Monolith.Scaffold.Apps;
using Tsikhanau.Monolith.Scaffold.Cli;
using Tsikhanau.Monolith.Scaffold.Events;
using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Monolith.Scaffold.InternalCommands;
using Tsikhanau.Monolith.Scaffold.Modules;
using Tsikhanau.Monolith.Scaffold.Sources;
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
        var testTemplate = new FeatureTestTemplate(feature);
        IReadOnlyList<NewFile> featureFiles =
        [
            .. new FeatureTemplate(feature).CreateFiles(),
            testTemplate.CreateFile(),
        ];

        return Result
            .Collect(EnsureNoSuchFeature(feature, featureFiles, fileSystem))
            .And(VerifyRoute(feature, fileSystem))
            .And(VerifyInternalCommandName(feature, fileSystem))
            .And(new FacadeUpdate(feature.Module, fileSystem).Plan(feature.GetRequiredShape))
            .And(new NeighbourReferenceUpdate(feature.Module, fileSystem).Plan(feature.Neighbour))
            .Bind((_, _, nameNote, facadeChange, referenceEdits) =>
                CombinePlan(
                    feature,
                    facadeChange,
                    referenceEdits,
                    featureFiles,
                    BuildNextStep(feature, testTemplate),
                    nameNote));
    }

    private static Result<ScaffoldPlan> CombinePlan(
        ScaffoldedFeature feature,
        FacadeChange facadeChange,
        IReadOnlyList<FileEdit> referenceEdits,
        IReadOnlyList<NewFile> featureFiles,
        String nextStep,
        Optional<String> nameNote) =>
        CodeLayout
            .EnsureLinesFit([.. facadeChange.NewFiles, .. featureFiles], feature.ChosenNames, feature.ShorterChoice)
            .Map(newFiles => new ScaffoldPlan(newFiles, [.. facadeChange.FileEdits, .. referenceEdits], [], nextStep)
            {
                Note = nameNote,
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

    private static String BuildNextStep(ScaffoldedFeature feature, FeatureTestTemplate testTemplate) =>
        $"the new test {feature.TestClass}.{testTemplate.TestMethod} fails with "
        + $"\"{feature.NotImplementedMessage}\" until the handler does its work; next, run ./gate.sh";
}
