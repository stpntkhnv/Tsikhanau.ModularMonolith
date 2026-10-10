using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Api.Modules;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Monolith.TestModules.Pings.Domain.Pings;
using Tsikhanau.Monolith.TestModules.Pings.Features;
using Tsikhanau.Monolith.TestModules.Pings.Infrastructure;
using Tsikhanau.Monolith.TestModules.Pings.Infrastructure.Persistence;
using Tsikhanau.Monolith.TestModules.Pings.Settings;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings;

public sealed class PingsModule : Module
{
    protected override void AddServices(IServiceCollection services) =>
        AddSettings<PingsSettings>(services)
            .AddModuleInfrastructure<IPingsModule, PingsModuleFacade, PingsDbContext>(ConstraintErrors)
            .AddScoped<IPingRepository, PingRepository>()
            .AddSingleton<PingTestHooks>();

    private static Dictionary<String, Error> ConstraintErrors =>
        new() { [PingConfiguration.LabelIndex] = PingErrors.LabelTaken };
}
