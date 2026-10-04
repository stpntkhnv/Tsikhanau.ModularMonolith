using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Railway;

namespace ModularMonolith.BuildingBlocks.Api.Modules;

public abstract class Module : IModule
{
    private Optional<IConfigurationSection> _settingsSection;

    public abstract String Name { get; }

    void IModule.Register(IServiceCollection services, IConfigurationSection settingsSection)
    {
        _settingsSection = Optional<IConfigurationSection>.Some(settingsSection);
        AddServices(services);
    }

    public abstract void MapEndpoints(IEndpointRouteBuilder endpoints);

    public virtual void UsePipeline(IApplicationBuilder app) { }

    protected abstract void AddServices(IServiceCollection services);

    protected IServiceCollection AddSettings<TSettings>(IServiceCollection services)
        where TSettings : class
    {
        services
            .AddOptions<TSettings>()
            .Bind(_settingsSection.GetValueOrDefault(ThrowSettingsOutsideRegister))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }

    private IConfigurationSection ThrowSettingsOutsideRegister() =>
        throw new InvalidOperationException($"Module {Name} adds settings outside Register");
}
