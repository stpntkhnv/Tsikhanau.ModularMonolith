using System.Globalization;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Infrastructure.Queues.CommandLine;

internal sealed record FailedMessageRow(
    Guid Id,
    String Type,
    Int32 Attempts,
    DateTime FailedOn,
    String? LastError,
    Boolean ContainsSecrets)
{
    public String BuildLine(String moduleName, String queue, ISecretRowRule secretRowRule) =>
        String.Create(
            CultureInfo.InvariantCulture,
            $"{moduleName}\t{queue}\t{Id}\t{Type}\t{Attempts}\t{FailedOn:O}\t{FindCode(secretRowRule)}");

    private String FindCode(ISecretRowRule secretRowRule)
    {
        var isSecret = secretRowRule.IsSecret(Type, ContainsSecrets);
        return Optional<String>
            .FromNullable(LastError)
            .Bind(lastError => HandlingFailure.FindCode(lastError, isSecret))
            .GetValueOrDefault(String.Empty);
    }
}
