using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Plans;

internal sealed record ScaffoldPlan(
    IReadOnlyList<NewFile> NewFiles,
    IReadOnlyList<FileEdit> FileEdits,
    IReadOnlyList<ExternalStep> ExternalSteps,
    String NextStep)
{
    public Optional<String> Note { get; init; } = Optional<String>.None();
}
