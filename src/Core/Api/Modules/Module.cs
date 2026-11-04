using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Api.Endpoints;
using Tsikhanau.Monolith.Application.Modules;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Api.Modules;

public abstract class Module : IModule
{
    private Optional<IConfigurationSection> _settingsSection;

    public String Name => ModuleName.FromModuleClass(GetType());

    void IModule.Register(IServiceCollection services, IConfigurationSection settingsSection)
    {
        _settingsSection = Optional<IConfigurationSection>.Some(settingsSection);
        AddServices(services);
    }

    public virtual void MapEndpoints(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGroup(RoutePrefix)
            .WithTags(Name)
            .MapEndpointsOf(GetType());

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

    private String RoutePrefix => $"/{JsonNamingPolicy.KebabCaseLower.ConvertName(Name)}";

    private IConfigurationSection ThrowSettingsOutsideRegister() =>
        throw new InvalidOperationException($"Module {Name} adds settings outside Register");
}
