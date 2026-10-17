using System.Globalization;

namespace Tsikhanau.Monolith.Infrastructure.Queues.CommandLine;

internal sealed record FailedMessageRow(
    Guid Id,
    String Type,
    Int32 Attempts,
    DateTime FailedOn,
    String? LastError,
    Boolean ContainsSecrets,
    Boolean HasErasedPayload)
{
    public String BuildLine(String moduleName, String queue, ISecretRowRule secretRowRule) =>
        String.Create(
            CultureInfo.InvariantCulture,
            $"{moduleName}\t{queue}\t{Id}\t{Type}\t{Attempts}\t{FailedOn:O}\t{FindCode(secretRowRule)}");

    private String FindCode(ISecretRowRule secretRowRule) =>
        LastError is null || secretRowRule.MayQuoteItsSecret(Type, ContainsSecrets, HasErasedPayload)
            ? String.Empty
            : HandlingFailure
                .FindCode(LastError)
                .GetValueOrDefault(String.Empty);
}
