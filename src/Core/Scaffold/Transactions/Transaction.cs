using System.Text;
using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Transactions;

internal sealed class Transaction(IFileSystem fileSystem, IProcessRunner processRunner)
{
    private const String RolledBack = "files scaffold created are deleted, files it changed are back as they were";

    private const String RollBackUnfinished = "the rollback did not finish, restore these by hand";

    private static readonly Error Stopped = Error.Failure("scaffold.stopped", "scaffold was stopped");

    private readonly Journal _journal = new();

    public async Task<Result<Unit>> RunAsync(ScaffoldPlan plan, CancellationToken cancellationToken)
    {
        Result<Unit> outcome;
        try
        {
            outcome = await ApplyAsync(plan, cancellationToken);
        }
        catch (Exception exception)
        {
            var rollBack = _journal.RollBack(fileSystem);
            if (rollBack.IsFailure)
            {
                throw new InvalidOperationException($"{RollBackUnfinished}: {rollBack.Error.Message}", exception);
            }

            throw;
        }

        return outcome.Match(_ => Commit(), RollBackAfter);
    }

    private Task<Result<Unit>> ApplyAsync(ScaffoldPlan plan, CancellationToken cancellationToken) =>
        EnsureEachFilePlannedOnce(plan)
            .Bind(_ => EnsureUntouched(plan))
            .Bind(_ => CreateFiles(plan.NewFiles, cancellationToken))
            .Bind(_ => ChangeFiles(plan.FileEdits, cancellationToken))
            .BindAsync(_ => RunStepsAsync(plan.ExternalSteps, cancellationToken));

    private static Result<Unit> EnsureEachFilePlannedOnce(ScaffoldPlan plan)
    {
        var problems = GetRepeatedPaths(plan)
            .Select(path => $"{path} is planned more than once; a plan writes each file once")
            .ToList();

        return problems.Count == 0
            ? Unit.Value
            : Error.Failure("scaffold.plan", String.Join("; ", problems));
    }

    private static IEnumerable<String> GetRepeatedPaths(ScaffoldPlan plan) =>
        plan.NewFiles
            .Select(newFile => newFile.Path)
            .Concat(plan.FileEdits.Select(edit => edit.Path))
            .GroupBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Where(paths => paths.Count() > 1)
            .Select(paths => paths.Key);

    private Result<Unit> EnsureUntouched(ScaffoldPlan plan)
    {
        var problems = plan.NewFiles
            .Where(Exists)
            .Select(newFile => $"{newFile.Path} already exists")
            .Concat(plan.FileEdits
                .Where(HasChanged)
                .Select(edit => $"{edit.Path} changed after scaffold read it"))
            .ToList();

        return problems.Count == 0
            ? Unit.Value
            : Error.Failure("scaffold.changed", String.Join("; ", problems));
    }

    private Boolean Exists(NewFile newFile) => fileSystem.FileExists(newFile.Path);

    private Boolean HasChanged(FileEdit edit) => !fileSystem.HasContent(edit.Path, edit.Original);

    private Result<Unit> CreateFiles(IReadOnlyList<NewFile> newFiles, CancellationToken cancellationToken) =>
        newFiles.Aggregate(
            Result.Success(),
            (result, newFile) => result.Bind(_ => CreateFile(newFile, cancellationToken)));

    private Result<Unit> CreateFile(NewFile newFile, CancellationToken cancellationToken) =>
        EnsureNotStopped(cancellationToken)
            .Bind(_ => CreateFolders(GetFolder(newFile.Path)))
            .Tap(_ => _journal.Record(new CreatedFile(newFile.Path)))
            .Bind(_ => fileSystem.Attempt(
                () => fileSystem.WriteAllBytes(newFile.Path, Encoding.UTF8.GetBytes(newFile.Content)),
                $"cannot write {newFile.Path}"));

    private Result<Unit> CreateFolders(String folder) =>
        GetMissingFolders(folder)
            .Reverse()
            .Aggregate(Result.Success(), (result, missingFolder) => result.Bind(_ => CreateFolder(missingFolder)));

    private IEnumerable<String> GetMissingFolders(String folder)
    {
        for (var current = folder; IsMissing(current); current = GetFolder(current))
        {
            yield return current;
        }
    }

    private Boolean IsMissing(String folder) => folder.Length > 0 && !fileSystem.DirectoryExists(folder);

    private Result<Unit> CreateFolder(String folder) =>
        fileSystem
            .Attempt(() => fileSystem.CreateDirectory(folder), $"cannot create the folder {folder}")
            .Tap(_ => _journal.Record(new CreatedDirectory(folder)));

    private Result<Unit> ChangeFiles(IReadOnlyList<FileEdit> edits, CancellationToken cancellationToken) =>
        edits.Aggregate(
            Result.Success(),
            (result, edit) => result.Bind(_ => ChangeFile(edit, cancellationToken)));

    private Result<Unit> ChangeFile(FileEdit edit, CancellationToken cancellationToken) =>
        EnsureNotStopped(cancellationToken)
            .Tap(_ => _journal.Record(new ChangedFile(edit.Path, edit.Original)))
            .Bind(_ => fileSystem.Attempt(
                () => fileSystem.WriteAllBytes(edit.Path, edit.Updated),
                $"cannot write {edit.Path}"));

    private async Task<Result<Unit>> RunStepsAsync(
        IReadOnlyList<ExternalStep> steps,
        CancellationToken cancellationToken)
    {
        foreach (var step in steps)
        {
            var result = await EnsureNotStopped(cancellationToken)
                .BindAsync(_ => processRunner.RunAsync(step, cancellationToken));

            if (result.IsFailure)
            {
                return result;
            }
        }

        return Unit.Value;
    }

    private Result<Unit> Commit()
    {
        _journal.Clear();
        return Unit.Value;
    }

    private Result<Unit> RollBackAfter(Error error) =>
        _journal.IsEmpty
            ? error
            : _journal
                .RollBack(fileSystem)
                .Match(
                    _ => Error.Failure(error.Code, $"{error.Message}; {RolledBack}"),
                    rollBackError => Error.Failure(
                        error.Code,
                        $"{error.Message}; {RollBackUnfinished}: {rollBackError.Message}"));

    private static Result<Unit> EnsureNotStopped(CancellationToken cancellationToken) =>
        cancellationToken.IsCancellationRequested ? Stopped : Unit.Value;

    private static String GetFolder(String path) => Path.GetDirectoryName(path) ?? String.Empty;
}
