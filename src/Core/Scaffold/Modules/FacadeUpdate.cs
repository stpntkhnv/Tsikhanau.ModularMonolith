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

    public Result<FacadeChange> Plan(Func<FacadeShape, FacadeShape> require)
    {
        var shape = ReadShape();
        var requiredShape = require(shape);

        return requiredShape == shape
            ? FacadeChange.None
            : PlanEdits(shape, requiredShape)
                .Map(fileEdits => new FacadeChange(CreateMarkerFiles(shape, requiredShape), fileEdits));
    }

    private FacadeShape ReadShape() => new(HasCommands: fileSystem.FileExists(_facadeTemplate.CommandMarkerPath));

    private IReadOnlyList<NewFile> CreateMarkerFiles(FacadeShape shape, FacadeShape requiredShape) =>
        requiredShape.HasCommands && !shape.HasCommands ? [_facadeTemplate.CommandMarkerFile] : [];

    private Result<IReadOnlyList<FileEdit>> PlanEdits(FacadeShape shape, FacadeShape requiredShape) =>
        Result.CombineAll(
            PlanEdit(
                _facadeTemplate.FacadeContractPath,
                _facadeTemplate.RenderFacadeContract(shape),
                _facadeTemplate.RenderFacadeContract(requiredShape)),
            PlanEdit(
                _facadeTemplate.FacadeClassPath,
                _facadeTemplate.RenderFacadeClass(shape),
                _facadeTemplate.RenderFacadeClass(requiredShape)));

    private Result<FileEdit> PlanEdit(String path, String scaffolded, String rewritten) =>
        fileSystem
            .ReadTextFile(path)
            .Bind(facade => FacadeEdit.Rewrite(facade, scaffolded, rewritten))
            .MapError(error => Error.Failure(error.Code, $"{error.Message}, {CannotAddCommands}"));
}
