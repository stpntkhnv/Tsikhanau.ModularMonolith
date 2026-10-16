using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Infrastructure.Modules;

namespace Tsikhanau.Monolith.Infrastructure.Queues.CommandLine;

internal static class SecretRowRuleExtensions
{
    extension(IServiceProvider serviceProvider)
    {
        public ISecretRowRule ResolveSecretRowRule(ModuleDescriptor module, String queue) =>
            serviceProvider
                .GetServices<ISecretRowRule>()
                .Single(secretRowRule => secretRowRule.ModuleKey == module.Key && secretRowRule.Table == queue);
    }
}
