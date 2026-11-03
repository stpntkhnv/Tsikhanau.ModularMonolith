using Tsikhanau.Monolith.Contracts.Secrets;

namespace Tsikhanau.Monolith.Infrastructure.Queues.Secrets;

internal static class SecretTypeExtensions
{
    extension(Type type)
    {
        public Boolean ContainsSecrets => type.IsDefined(typeof(ContainsSecretsAttribute), inherit: false);
    }
}
