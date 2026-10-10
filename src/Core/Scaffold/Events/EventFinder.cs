using Microsoft.CodeAnalysis.CSharp.Syntax;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Events;

internal sealed class EventFinder(IReadOnlyList<SourceProject> projects)
{
    private const String EventMark = "Event";

    private static readonly Dictionary<String, EventKind> EventBases = new(StringComparer.Ordinal)
    {
        ["DomainEvent"] = EventKind.Domain,
        ["IntegrationEvent"] = EventKind.Integration,
    };

    public Result<EventDeclaration> Find(String moduleName, String eventType)
    {
        var candidates = projects
            .SelectMany(project => FindCandidates(project, eventType.Split('.')[^1]))
            .Where(candidate => candidate.IsNamedBy(eventType))
            .OrderBy(candidate => candidate.FullName, StringComparer.Ordinal)
            .ToList();

        return candidates switch
        {
            [] => BuildNotFoundError(moduleName, eventType),
            [var candidate] => EnsureHandledBy(candidate, moduleName)
                .Bind(BuildDeclaration),
            _ => BuildAmbiguousError(eventType, candidates),
        };
    }

    private static IEnumerable<EventCandidate> FindCandidates(SourceProject project, String name) =>
        project
            .FindDeclarations<TypeDeclarationSyntax>(name)
            .Where(declaration => declaration.Name == name)
            .Select(declaration => new EventCandidate(project, declaration, FindKind(declaration)));

    private static Optional<EventKind> FindKind(TypeDeclarationSyntax declaration) =>
        declaration.BaseTypes
            .Select(baseType => baseType.SimpleName)
            .Where(EventBases.ContainsKey)
            .Select(baseName => Optional<EventKind>.Some(EventBases[baseName]))
            .FirstOrDefault();

    private static Result<EventCandidate> EnsureHandledBy(EventCandidate candidate, String moduleName) =>
        candidate.Kind.Match(
            kind => EnsureHandledBy(candidate, kind, moduleName),
            () => BuildError(
                $"{candidate.FullName} in {candidate.Project.Role} is not an event: it derives from neither "
                + "DomainEvent nor IntegrationEvent"));

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

    private Error BuildNotFoundError(String moduleName, String eventType)
    {
        var events = projects
            .SelectMany(FindEvents)
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

    private static IEnumerable<EventCandidate> FindEvents(SourceProject project) =>
        project
            .FindDeclarations<TypeDeclarationSyntax>(EventMark)
            .Select(declaration => new EventCandidate(project, declaration, FindKind(declaration)))
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

        public Boolean IsNamedBy(String eventType) => !eventType.Contains('.') || FullName == eventType;

        public Boolean CanBeHandledBy(String moduleName) =>
            Kind.Value == EventKind.Domain
                ? !Project.IsContracts && Project.ModuleName == moduleName
                : Project.IsContracts && Project.ModuleName != moduleName;
    }
}
