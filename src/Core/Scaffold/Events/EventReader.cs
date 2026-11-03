using Microsoft.CodeAnalysis.CSharp.Syntax;
using Tsikhanau.Monolith.Scaffold.Sources;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Events;

internal sealed class EventReader(IReadOnlyList<SourceProject> projects)
{
    private const String EventMark = "Event";

    private const Int32 MaxBaseDepth = 8;

    private static readonly Dictionary<String, EventKind> EventBases = new(StringComparer.Ordinal)
    {
        ["DomainEvent"] = EventKind.Domain,
        ["IntegrationEvent"] = EventKind.Integration,
    };

    public Result<EventDeclaration> Read(String moduleName, String eventType)
    {
        var name = eventType.Split('.')[^1];
        var candidates = projects
            .SelectMany(project => GetCandidates(project, name))
            .Where(candidate => IsNamedBy(candidate.FullName, eventType))
            .OrderBy(candidate => candidate.FullName, StringComparer.Ordinal)
            .ToList();

        var fullNames = candidates
            .Select(candidate => candidate.FullName)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return (candidates, fullNames) switch
        {
            ([], _) => BuildNotFoundError(moduleName, eventType, name),
            ([var candidate], _) => EnsureHandledBy(candidate, moduleName)
                .Bind(EnsureConcrete)
                .Bind(BuildDeclaration),
            (_, [var fullName]) => BuildError(
                $"{fullName} is declared more than once (partial); scaffold reads an event from one declaration"),
            _ => BuildAmbiguousError(eventType, candidates.DistinctBy(candidate => candidate.FullName)),
        };
    }

    private static Boolean IsNamedBy(String fullName, String eventType) =>
        !eventType.Contains('.') || fullName == eventType;

    private static IEnumerable<EventCandidate> GetCandidates(SourceProject project, String name) =>
        project
            .GetDeclarations<TypeDeclarationSyntax>(name)
            .Where(declaration => declaration.Name == name)
            .Select(declaration => new EventCandidate(project, declaration, FindKind(project, declaration)));

    private static Optional<EventKind> FindKind(SourceProject project, TypeDeclarationSyntax declaration) =>
        FindKind(project, declaration, baseDepth: 0);

    private static Optional<EventKind> FindKind(
        SourceProject project,
        TypeDeclarationSyntax declaration,
        Int32 baseDepth) =>
        declaration.BaseTypes
            .Select(baseType => FindKindOfBase(project, baseType.SimpleName, baseDepth))
            .FirstOrDefault(kind => kind.HasValue);

    private static Optional<EventKind> FindKindOfBase(SourceProject project, String baseName, Int32 baseDepth) =>
        baseName switch
        {
            _ when EventBases.TryGetValue(baseName, out var kind) => kind,
            _ when baseDepth < MaxBaseDepth => project
                .FindDeclaration<TypeDeclarationSyntax>(baseName)
                .Bind(baseDeclaration => FindKind(project, baseDeclaration, baseDepth + 1)),
            _ => Optional<EventKind>.None(),
        };

    private static Result<EventCandidate> EnsureHandledBy(EventCandidate candidate, String moduleName) =>
        candidate.Kind.Match(
            kind => EnsureHandledBy(candidate, kind, moduleName),
            () => BuildNotAnEventError(candidate));

    private static Error BuildNotAnEventError(EventCandidate candidate)
    {
        var role = candidate.Project.Role;
        var baseNames = candidate.Declaration.BaseTypes
            .Select(baseType => baseType.SimpleName)
            .ToList();

        return baseNames.Count == 0
            ? BuildError(
                $"{candidate.FullName} in {role} is not an event: it derives from neither DomainEvent nor "
                + "IntegrationEvent")
            : BuildError(
                $"{candidate.FullName} in {role} is not an event scaffold can read: its base list names "
                + $"{String.Join(", ", baseNames)}, which scaffold cannot follow to DomainEvent or IntegrationEvent "
                + $"through the declarations of {role}");
    }

    private static Result<EventCandidate> EnsureHandledBy(EventCandidate candidate, EventKind kind, String moduleName)
    {
        var project = candidate.Project;
        var owner = project.ModuleName;

        return (kind, project.IsContracts, owner == moduleName) switch
        {
            (EventKind.Domain, false, true) or (EventKind.Integration, true, false) => candidate,
            (EventKind.Domain, false, false) => BuildError(
                $"{candidate.FullName} is a domain event of {owner}: a domain event never leaves its module; "
                + $"handle the integration event that {owner} publishes for it"),
            (EventKind.Integration, true, true) => BuildError(
                $"{candidate.FullName} is an integration event of {owner} itself: a module reacts to its own "
                + "changes through the domain event it publishes the integration event from"),
            _ => BuildError($"{candidate.FullName} sits in {project.Role}, where {Describe(kind)} does not belong"),
        };
    }

    private static Result<EventCandidate> EnsureConcrete(EventCandidate candidate) =>
        candidate.Declaration.IsAbstract
            ? BuildError(
                $"{candidate.FullName} is abstract, and the generated test builds the event it handles; "
                + "pass a concrete event")
            : candidate;

    private static Result<EventDeclaration> BuildDeclaration(EventCandidate candidate)
    {
        var declaration = candidate.Declaration;
        var memberSamples = new MemberSamples(candidate.Project);
        var samples = declaration.PositionalParameters
            .Select(parameter => (Parameter: parameter, Sample: memberSamples.Build(parameter)))
            .ToList();

        var unsupported = samples
            .Where(member => member.Sample.IsNone)
            .Select(member => $"{declaration.Name}.{member.Parameter.Identifier.Text} ({member.Parameter.Type})")
            .ToList();

        return (samples.Count, unsupported.Count) switch
        {
            (0, _) => BuildError(
                $"{candidate.FullName} has no positional members; scaffold builds the event in the generated test "
                + "through its primary constructor"),
            (_, 0) => new EventDeclaration(
                declaration.Name,
                declaration.Namespace,
                candidate.Kind.Value,
                candidate.Project.ModuleName,
                [.. samples.Select(member => member.Sample.Value)]),
            _ => BuildError(
                $"cannot build a sample of {String.Join(", ", unsupported)} for the generated test; "
                + MemberSamples.SupportedTypes),
        };
    }

    private Error BuildNotFoundError(String moduleName, String eventType, String name) =>
        FindNestedError(eventType, name)
            .GetValueOrDefault(() => BuildNoSuchTypeError(moduleName, eventType));

    private Optional<Error> FindNestedError(String eventType, String name) =>
        projects
            .SelectMany(project => project.GetNodes<BaseTypeDeclarationSyntax>(name))
            .Where(declaration => declaration.Name == name && !declaration.IsTopLevel)
            .Where(declaration => IsNamedBy(declaration.FullName, eventType))
            .OrderBy(declaration => declaration.FullName, StringComparer.Ordinal)
            .Select(BuildNestedError)
            .FirstOrDefault();

    private static Error BuildNestedError(BaseTypeDeclarationSyntax nestedDeclaration) =>
        BuildError(
            $"{nestedDeclaration.FullName} is nested in {String.Join('.', nestedDeclaration.ContainingTypeNames)}; "
            + "scaffold handles only top-level types, one per file (STYLE 7)");

    private Error BuildNoSuchTypeError(String moduleName, String eventType)
    {
        var events = projects
            .SelectMany(GetEvents)
            .Where(candidate => candidate.CanBeHandledBy(moduleName))
            .Select(candidate => candidate.Declaration.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        var found = events.Count == 0 ? "none" : String.Join(", ", events);
        return BuildError(
            $"--event {eventType}: no such type; scaffold looks for a domain event in the module project of "
            + $"{moduleName} and for an integration event in the Contracts of the other modules (found: {found})");
    }

    private static IEnumerable<EventCandidate> GetEvents(SourceProject project) =>
        project
            .GetDeclarations<TypeDeclarationSyntax>(EventMark)
            .Select(declaration => new EventCandidate(project, declaration, FindKind(project, declaration)))
            .Where(candidate => candidate.Kind.HasValue);

    private static Error BuildAmbiguousError(String eventType, IEnumerable<EventCandidate> candidates) =>
        BuildError(
            $"--event {eventType} names more than one type: "
            + String.Join(", ", candidates.Select(candidate => $"{candidate.FullName} ({candidate.Project.Role})"))
            + "; pass the full name");

    private static String Describe(EventKind kind) =>
        kind == EventKind.Domain ? "a domain event" : "an integration event";

    private static Error BuildError(String message) => Error.Failure("scaffold.event", message);

    private sealed record EventCandidate(
        SourceProject Project,
        TypeDeclarationSyntax Declaration,
        Optional<EventKind> Kind)
    {
        public String FullName => Declaration.FullName;

        public Boolean CanBeHandledBy(String moduleName) =>
            Kind.Value == EventKind.Domain
                ? !Project.IsContracts && Project.ModuleName == moduleName
                : Project.IsContracts && Project.ModuleName != moduleName;
    }
}
