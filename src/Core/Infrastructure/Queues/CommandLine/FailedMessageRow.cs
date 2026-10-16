using System.Globalization;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Infrastructure.Queues.CommandLine;

internal sealed record FailedMessageRow(Guid Id, String Type, Int32 Attempts, DateTime FailedOn, String? LastError)
{
    public String BuildLine(String moduleName, String queue) =>
        String.Create(
            CultureInfo.InvariantCulture,
            $"{moduleName}\t{queue}\t{Id}\t{Type}\t{Attempts}\t{FailedOn:O}\t{FindCode()}");

    private String FindCode() =>
        Optional<String>
            .FromNullable(LastError)
            .Bind(HandlingFailure.FindCode)
            .GetValueOrDefault(String.Empty);
}
