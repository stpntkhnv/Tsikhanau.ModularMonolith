using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tsikhanau.Monolith.Api.Modules;

namespace Tsikhanau.Monolith.UnitTests.Modules;

public sealed class ModuleSettingsTests
{
    [Fact]
    public async Task Module_reads_only_its_own_section()
    {
        await using var app = CreateApp(
            new() { ["App:Modules:Sample:Limit"] = "3", ["App:Modules:Other:Limit"] = "5" });

        var settings = ResolveSettings(app);

        settings.Limit.ShouldBe(3);
    }

    [Fact]
    public async Task Invalid_module_settings_are_rejected()
    {
        await using var app = CreateApp(new() { ["App:Modules:Sample:Limit"] = "0" });

        Should.Throw<OptionsValidationException>(() => ResolveSettings(app));
    }

    [Fact]
    public async Task Missing_required_setting_fails_validation_on_start()
    {
        await using var app = CreateApp(new() { ["App:Modules:Other:Limit"] = "5" });

        var startupValidator = app.Services.GetRequiredService<IStartupValidator>();

        Should.Throw<OptionsValidationException>(startupValidator.Validate);
    }

    [Fact]
    public void Adding_settings_outside_register_fails_naming_the_module()
    {
        var sampleModule = new SampleModule();

        var exception = Should.Throw<InvalidOperationException>(
            () => sampleModule.AddSettingsOutsideRegister(new ServiceCollection()));

        exception.Message.ShouldBe("Module Sample adds settings outside Register");
    }

    private static WebApplication CreateApp(Dictionary<String, String?> configurationValues)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Configuration.AddInMemoryCollection(configurationValues);
        builder.AddModule<SampleModule>();
        return builder.Build();
    }

    private static SampleSettings ResolveSettings(WebApplication app) =>
        app.Services.GetRequiredService<IOptions<SampleSettings>>().Value;

    private sealed record SampleSettings
    {
        [Range(1, 5)]
        public required Int32 Limit { get; init; }
    }

    private sealed class SampleModule : Module
    {
        public IServiceCollection AddSettingsOutsideRegister(IServiceCollection services) =>
            AddSettings<SampleSettings>(services);

        protected override void AddServices(IServiceCollection services) => AddSettings<SampleSettings>(services);
    }
}
