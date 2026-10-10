using Tsikhanau.Monolith.Scaffold.Files;

namespace Tsikhanau.Monolith.Scaffold.Apps;

internal sealed record AppLayout(
    String ProjectPrefix,
    String RootNamespace,
    TextFile Solution,
    TextFile HostProject,
    TextFile Program,
    TextFile AppSettings);
