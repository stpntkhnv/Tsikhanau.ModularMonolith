using Tsikhanau.Monolith.Scaffold.Edits;
using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Monolith.Scaffold.Transactions;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Modules;

internal sealed class FacadeUpdate(ScaffoldedModule module, IFileSystem fileSystem)
{
    private readonly FacadeTemplate _facadeTemplate = new(module);

    private String CannotAddCommands =>
        $"so scaffold cannot add ExecuteCommandAsync over {module.CommandMarker} to it; restore it, "
        + $"or add {module.CommandMarker} and ExecuteCommandAsync by hand and run scaffold again";

    private String CannotAddQueries =>
        $"so scaffold cannot add ExecuteQueryAsync over {module.QueryMarker} to it; restore it, "
        + $"or add {module.QueryMarker}, {module.QueryFacade} and ExecuteQueryAsync by hand and run scaffold again";

    private String HalfQueryShape =>
        $"{module.ContractsFolder} must hold both {module.QueryMarker}.cs and {module.QueryFacade}.cs "
        + "or neither of them";

    public Result<FacadeChange> Plan(Func<FacadeShape, FacadeShape> require) =>
        ReadShape()
            .Bind(shape => PlanChange(shape, require(shape)));

    private Result<FacadeShape> ReadShape()
    {
        var hasQueryMarker = fileSystem.FileExists(_facadeTemplate.QueryMarkerPath);
        var hasQueryFacade = fileSystem.FileExists(_facadeTemplate.QueryFacadePath);

        return hasQueryMarker == hasQueryFacade
            ? new FacadeShape(fileSystem.FileExists(_facadeTemplate.CommandMarkerPath), hasQueryMarker)
            : Error.Failure("scaffold.anchor", HalfQueryShape);
    }

    private Result<FacadeChange> PlanChange(FacadeShape shape, FacadeShape requiredShape) =>
        requiredShape == shape
            ? FacadeChange.None
            : PlanEdits(shape, requiredShape)
                .Map(fileEdits => new FacadeChange(CreateMarkerFiles(shape, requiredShape), fileEdits));

    private IReadOnlyList<NewFile> CreateMarkerFiles(FacadeShape shape, FacadeShape requiredShape) =>
        [.. CreateCommandMarkerFiles(shape, requiredShape), .. CreateQueryMarkerFiles(shape, requiredShape)];

    private IEnumerable<NewFile> CreateCommandMarkerFiles(FacadeShape shape, FacadeShape requiredShape) =>
        AddsCommands(shape, requiredShape) ? [_facadeTemplate.CommandMarkerFile] : [];

    private IEnumerable<NewFile> CreateQueryMarkerFiles(FacadeShape shape, FacadeShape requiredShape) =>
        AddsQueries(shape, requiredShape) ? [_facadeTemplate.QueryMarkerFile, _facadeTemplate.QueryFacadeFile] : [];

    private Result<IReadOnlyList<FileEdit>> PlanEdits(FacadeShape shape, FacadeShape requiredShape)
    {
        var cannotAdd = AddsCommands(shape, requiredShape) ? CannotAddCommands : CannotAddQueries;

        return Result.CombineAll(
            PlanEdit(
                _facadeTemplate.FacadeContractPath,
                _facadeTemplate.RenderFacadeContract(shape),
                _facadeTemplate.RenderFacadeContract(requiredShape),
                cannotAdd),
            PlanEdit(
                _facadeTemplate.FacadeClassPath,
                _facadeTemplate.RenderFacadeClass(shape),
                _facadeTemplate.RenderFacadeClass(requiredShape),
                cannotAdd));
    }

    private Result<FileEdit> PlanEdit(String path, String scaffolded, String rewritten, String cannotAdd) =>
        fileSystem
            .ReadTextFile(path)
            .Bind(facade => FacadeEdit.Rewrite(facade, scaffolded, rewritten))
            .MapError(error => Error.Failure(error.Code, $"{error.Message}, {cannotAdd}"));

    private static Boolean AddsCommands(FacadeShape shape, FacadeShape requiredShape) =>
        requiredShape.HasCommands && !shape.HasCommands;

    private static Boolean AddsQueries(FacadeShape shape, FacadeShape requiredShape) =>
        requiredShape.HasQueries && !shape.HasQueries;
}
