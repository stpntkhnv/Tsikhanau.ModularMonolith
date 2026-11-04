using Tsikhanau.Monolith.Scaffold.Plans;

namespace Tsikhanau.Monolith.Scaffold.Features;

internal sealed record ModuleChanges(IReadOnlyList<NewFile> NewFiles, IReadOnlyList<FileEdit> FileEdits);
