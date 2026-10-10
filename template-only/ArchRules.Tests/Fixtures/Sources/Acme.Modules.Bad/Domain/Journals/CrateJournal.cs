using Microsoft.Extensions.Logging;

namespace Acme.Modules.Bad.Domain.Journals;

internal static class CrateJournal
{
    public static Boolean IsOpen(ILogger logger) => logger is not null;
}
