using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Tsikhanau.Monolith.Infrastructure.Database;
using Tsikhanau.Monolith.Infrastructure.Modules;

namespace Tsikhanau.Monolith.Infrastructure.Queues.CommandLine;

internal sealed record MessageAddress(ModuleDescriptor Module, String Queue, Guid MessageId)
{
    public String Table => $"\"{Module.Schema}\".{Queue}";

    public Task<NpgsqlConnection> OpenConnectionAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken) =>
        ModuleSignIn.OpenAsync(
            Module,
            serviceProvider.GetRequiredKeyedService<NpgsqlDataSource>(Module.Key),
            cancellationToken);

    public ISecretRowRule ResolveSecretRowRule(IServiceProvider serviceProvider) =>
        serviceProvider
            .GetServices<ISecretRowRule>()
            .Single(secretRowRule => secretRowRule.ModuleKey == Module.Key && secretRowRule.Table == Queue);

    public override String ToString() => $"{Module.Name} {Queue} {MessageId}";
}
