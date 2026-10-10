using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Monolith.Scaffold.Transactions;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Edits;

internal static class FacadeEdit
{
    public static Result<FileEdit> Rewrite(TextFile facade, String scaffolded, String rewritten) =>
        facade.PlanEdit(text =>
            ToUnixLineEndings(text) == scaffolded
                ? rewritten.Replace("\n", facade.NewLine, StringComparison.Ordinal)
                : Error.Failure("scaffold.anchor", $"{facade.Path} is not the file scaffold writes for this module"));

    private static String ToUnixLineEndings(String text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);
}
