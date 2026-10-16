using System.Globalization;

namespace Tsikhanau.Monolith.Infrastructure.Queues.CommandLine;

internal sealed record FailedMessageRow(Guid Id, String Type, Int32 Attempts, DateTime FailedOn, String Code)
{
    public String BuildLine(String moduleName, String queue) =>
        String.Create(
            CultureInfo.InvariantCulture,
            $"{moduleName}\t{queue}\t{Id}\t{Type}\t{Attempts}\t{FailedOn:O}\t{Code}");
}
