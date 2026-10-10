using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Api.Endpoints;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Api.Modules;

public abstract partial class Module : IModule
{
    private const String ModuleGroup = "Module";

    private const String NameProblem = "must be named <Module>Module, like NotesModule";

    private Optional<IConfigurationSection> _settingsSection;

    public String Name => GetName(GetType());

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

    private static String GetName(Type moduleType)
    {
        var moduleNamePattern = ModuleNamePattern();
        return moduleNamePattern.Match(moduleType.Name) is { Success: true } moduleName
            ? moduleName.Groups[ModuleGroup].Value
            : throw new InvalidOperationException($"Module class {moduleType.FullName} {NameProblem}");
    }

    private IConfigurationSection ThrowSettingsOutsideRegister() =>
        throw new InvalidOperationException($"Module {Name} adds settings outside Register");

    [GeneratedRegex($@"\A(?<{ModuleGroup}>\w+)Module\z")]
    private static partial Regex ModuleNamePattern();
}
