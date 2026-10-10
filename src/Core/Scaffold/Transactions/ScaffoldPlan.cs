namespace Tsikhanau.Monolith.Scaffold.Transactions;

internal sealed record ScaffoldPlan(
    IReadOnlyList<NewFile> NewFiles,
    IReadOnlyList<FileEdit> FileEdits,
    IReadOnlyList<ExternalStep> ExternalSteps,
    String NextStep = "next, run ./gate.sh and commit");
