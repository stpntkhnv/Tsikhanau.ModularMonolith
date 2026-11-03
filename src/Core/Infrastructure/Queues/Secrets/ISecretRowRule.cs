namespace Tsikhanau.Monolith.Infrastructure.Queues.Secrets;

internal interface ISecretRowRule
{
    Type ModuleKey { get; }

    String Table { get; }

    Boolean IsSecret(String type, Boolean containsSecrets);
}
