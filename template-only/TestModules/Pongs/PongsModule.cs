using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Api.Modules;
using Tsikhanau.Monolith.TestModules.Pongs.Infrastructure;
using Tsikhanau.Monolith.TestModules.Pongs.Settings;

namespace Tsikhanau.Monolith.TestModules.Pongs;

public sealed class PongsModule : Module
{
    protected override void AddServices(IServiceCollection services) =>
        AddSettings<PongsSettings>(services)
            .AddPongsInfrastructure();
}
