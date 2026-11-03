using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Monolith.Infrastructure.Queues.Processing;

namespace Tsikhanau.Monolith.Infrastructure.Queues.Secrets;

internal static class SecretRowRuleExtensions
{
    extension(IServiceProvider serviceProvider)
    {
        public ISecretRowRule ResolveSecretRowRule(ModuleDescriptor module, String queue) =>
            serviceProvider
                .GetServices<ISecretRowRule>()
                .Single(secretRowRule => secretRowRule.ModuleKey == module.Key && secretRowRule.Table == queue);
    }

    extension(ISecretRowRule secretRowRule)
    {
        public Boolean MayQuoteItsSecret(String type, Boolean containsSecrets, Boolean hasErasedPayload) =>
            !hasErasedPayload && secretRowRule.IsSecret(type, containsSecrets);
    }
}
