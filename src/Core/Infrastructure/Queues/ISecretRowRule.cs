namespace Tsikhanau.Monolith.Infrastructure.Queues;

internal interface ISecretRowRule
{
    Type ModuleKey { get; }

    String Table { get; }

    Boolean IsSecret(String type, Boolean containsSecrets);
}
