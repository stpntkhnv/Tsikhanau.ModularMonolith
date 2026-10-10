using Tsikhanau.Monolith.Scaffold.Modules;
using Tsikhanau.Monolith.Scaffold.Tests.Support;
using Tsikhanau.Monolith.Scaffold.Transactions;

namespace Tsikhanau.Monolith.Scaffold.Tests.Modules;

public sealed class ModuleTemplateTests
{
    private const String NotesFolder = "src/Modules/Notes/Acme.Shop.Modules.Notes";

    private const String LongName = "InventoryReservationAdjustmentApprovals";

    private const String LongFolder =
        "src/Modules/InventoryReservationAdjustmentApprovals/Acme.Shop.Modules.InventoryReservationAdjustmentApprovals";

    private const String MaxName = "Abbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Fact]
    public void Fresh_module_is_two_projects_and_five_wiring_files_without_markers()
    {
        var notes = SampleModules.Create("Notes");

        var files = new ModuleTemplate(notes, hasSettings: false).CreateFiles();

        files.ShouldBe(
        [
            new NewFile(
                "src/Modules/Notes/Acme.Shop.Modules.Notes.Contracts/Acme.Shop.Modules.Notes.Contracts.csproj",
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <ItemGroup>
                    <ProjectReference Include="..\..\..\Core\Contracts\Tsikhanau.Monolith.Contracts.csproj" />
                  </ItemGroup>
                </Project>

                """),
            new NewFile(
                $"{NotesFolder}/Acme.Shop.Modules.Notes.csproj",
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <ItemGroup>
                    <InternalsVisibleTo Include="Acme.Shop.IntegrationTests" />
                    <InternalsVisibleTo Include="Acme.Shop.UnitTests" />
                  </ItemGroup>
                  <ItemGroup>
                    <ProjectReference Include="..\Acme.Shop.Modules.Notes.Contracts\Acme.Shop.Modules.Notes.Contracts.csproj" />
                    <ProjectReference Include="..\..\..\Core\Domain\Tsikhanau.Monolith.Domain.csproj" />
                    <ProjectReference Include="..\..\..\Core\Application\Tsikhanau.Monolith.Application.csproj" />
                    <ProjectReference Include="..\..\..\Core\Infrastructure\Tsikhanau.Monolith.Infrastructure.csproj" />
                    <ProjectReference Include="..\..\..\Core\Api\Tsikhanau.Monolith.Api.csproj" />
                  </ItemGroup>
                  <ItemGroup>
                    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" PrivateAssets="all" />
                  </ItemGroup>
                </Project>

                """),
            new NewFile(
                $"{NotesFolder}/NotesModule.cs",
                """
                using Acme.Shop.Modules.Notes.Infrastructure;
                using Acme.Shop.Modules.Notes.Infrastructure.Persistence;
                using Microsoft.Extensions.DependencyInjection;
                using Tsikhanau.Monolith.Api.Modules;
                using Tsikhanau.Monolith.Infrastructure.Modules;

                namespace Acme.Shop.Modules.Notes;

                public sealed class NotesModule : Module
                {
                    protected override void AddServices(IServiceCollection services) =>
                        services.AddModuleInfrastructure<INotesModule, NotesModuleFacade, NotesDbContext>();
                }

                """),
            new NewFile(
                $"{NotesFolder}/INotesModule.cs",
                """
                using Tsikhanau.Monolith.Application.Modules;

                namespace Acme.Shop.Modules.Notes;

                internal interface INotesModule : IModuleFacade;

                """),
            new NewFile(
                $"{NotesFolder}/Infrastructure/NotesModuleFacade.cs",
                """
                using Microsoft.Extensions.DependencyInjection;
                using Tsikhanau.Monolith.Infrastructure.Dispatching;

                namespace Acme.Shop.Modules.Notes.Infrastructure;

                internal sealed class NotesModuleFacade(IServiceScopeFactory serviceScopeFactory)
                    : ModuleFacade<INotesModule>(serviceScopeFactory), INotesModule;

                """),
            new NewFile(
                $"{NotesFolder}/Infrastructure/Persistence/NotesDbContext.cs",
                """
                using Microsoft.EntityFrameworkCore;
                using Tsikhanau.Monolith.Infrastructure.Persistence;

                namespace Acme.Shop.Modules.Notes.Infrastructure.Persistence;

                internal sealed class NotesDbContext(DbContextOptions<NotesDbContext> dbContextOptions)
                    : ModuleDbContext(dbContextOptions)
                {
                    public const String SchemaName = "notes";
                }

                """),
            new NewFile(
                $"{NotesFolder}/Infrastructure/Persistence/NotesDbContextDesignTimeFactory.cs",
                """
                using Tsikhanau.Monolith.Infrastructure.Persistence;

                namespace Acme.Shop.Modules.Notes.Infrastructure.Persistence;

                internal sealed class NotesDbContextDesignTimeFactory : ModuleDbContextDesignTimeFactory<NotesDbContext>;

                """),
        ]);
    }

    [Fact]
    public void Long_name_wraps_every_declaration_that_would_pass_120_characters()
    {
        var module = SampleModules.Create(LongName);

        var files = new ModuleTemplate(module, hasSettings: false)
            .CreateFiles()
            .Where(newFile => newFile.Path.EndsWith(".cs", StringComparison.Ordinal))
            .Where(newFile => Path.GetFileName(newFile.Path) != $"I{LongName}Module.cs");

        files.ShouldBe(
        [
            new NewFile(
                $"{LongFolder}/{LongName}Module.cs",
                """
                using Acme.Shop.Modules.InventoryReservationAdjustmentApprovals.Infrastructure;
                using Acme.Shop.Modules.InventoryReservationAdjustmentApprovals.Infrastructure.Persistence;
                using Microsoft.Extensions.DependencyInjection;
                using Tsikhanau.Monolith.Api.Modules;
                using Tsikhanau.Monolith.Infrastructure.Modules;

                namespace Acme.Shop.Modules.InventoryReservationAdjustmentApprovals;

                public sealed class InventoryReservationAdjustmentApprovalsModule : Module
                {
                    protected override void AddServices(IServiceCollection services) =>
                        services.AddModuleInfrastructure<
                            IInventoryReservationAdjustmentApprovalsModule,
                            InventoryReservationAdjustmentApprovalsModuleFacade,
                            InventoryReservationAdjustmentApprovalsDbContext>();
                }

                """),
            new NewFile(
                $"{LongFolder}/Infrastructure/{LongName}ModuleFacade.cs",
                """
                using Microsoft.Extensions.DependencyInjection;
                using Tsikhanau.Monolith.Infrastructure.Dispatching;

                namespace Acme.Shop.Modules.InventoryReservationAdjustmentApprovals.Infrastructure;

                internal sealed class InventoryReservationAdjustmentApprovalsModuleFacade(IServiceScopeFactory serviceScopeFactory)
                    : ModuleFacade<IInventoryReservationAdjustmentApprovalsModule>(serviceScopeFactory),
                        IInventoryReservationAdjustmentApprovalsModule;

                """),
            new NewFile(
                $"{LongFolder}/Infrastructure/Persistence/{LongName}DbContext.cs",
                """
                using Microsoft.EntityFrameworkCore;
                using Tsikhanau.Monolith.Infrastructure.Persistence;

                namespace Acme.Shop.Modules.InventoryReservationAdjustmentApprovals.Infrastructure.Persistence;

                internal sealed class InventoryReservationAdjustmentApprovalsDbContext(
                    DbContextOptions<InventoryReservationAdjustmentApprovalsDbContext> dbContextOptions)
                    : ModuleDbContext(dbContextOptions)
                {
                    public const String SchemaName = "inventory_reservation_adjustment_approvals";
                }

                """),
            new NewFile(
                $"{LongFolder}/Infrastructure/Persistence/{LongName}DbContextDesignTimeFactory.cs",
                """
                using Tsikhanau.Monolith.Infrastructure.Persistence;

                namespace Acme.Shop.Modules.InventoryReservationAdjustmentApprovals.Infrastructure.Persistence;

                internal sealed class InventoryReservationAdjustmentApprovalsDbContextDesignTimeFactory
                    : ModuleDbContextDesignTimeFactory<InventoryReservationAdjustmentApprovalsDbContext>;

                """),
        ]);
    }

    [Fact]
    public void Module_with_settings_adds_an_empty_settings_record_and_binds_it_first()
    {
        var notes = SampleModules.Create("Notes");
        var filesWithoutSettings = new ModuleTemplate(notes, hasSettings: false).CreateFiles();

        var files = new ModuleTemplate(notes, hasSettings: true).CreateFiles();

        var replacedPaths = filesWithoutSettings
            .Except(files)
            .Select(newFile => newFile.Path);

        files
            .Except(filesWithoutSettings)
            .ShouldBe(
            [
                new NewFile(
                    $"{NotesFolder}/NotesModule.cs",
                    """
                    using Acme.Shop.Modules.Notes.Infrastructure;
                    using Acme.Shop.Modules.Notes.Infrastructure.Persistence;
                    using Acme.Shop.Modules.Notes.Settings;
                    using Microsoft.Extensions.DependencyInjection;
                    using Tsikhanau.Monolith.Api.Modules;
                    using Tsikhanau.Monolith.Infrastructure.Modules;

                    namespace Acme.Shop.Modules.Notes;

                    public sealed class NotesModule : Module
                    {
                        protected override void AddServices(IServiceCollection services) =>
                            AddSettings<NotesSettings>(services)
                                .AddModuleInfrastructure<INotesModule, NotesModuleFacade, NotesDbContext>();
                    }

                    """),
                new NewFile(
                    $"{NotesFolder}/Settings/NotesSettings.cs",
                    """
                    namespace Acme.Shop.Modules.Notes.Settings;

                    internal sealed record NotesSettings;

                    """),
            ]);

        replacedPaths.ShouldBe([$"{NotesFolder}/NotesModule.cs"]);
    }

    [Fact]
    public void Long_name_with_settings_wraps_the_infrastructure_type_arguments()
    {
        var module = SampleModules.Create(LongName);

        var moduleClass = new ModuleTemplate(module, hasSettings: true)
            .CreateFiles()
            .Single(newFile => newFile.Path.EndsWith($"/{LongName}Module.cs", StringComparison.Ordinal))
            .Content;

        moduleClass.ShouldBe(
            """
            using Acme.Shop.Modules.InventoryReservationAdjustmentApprovals.Infrastructure;
            using Acme.Shop.Modules.InventoryReservationAdjustmentApprovals.Infrastructure.Persistence;
            using Acme.Shop.Modules.InventoryReservationAdjustmentApprovals.Settings;
            using Microsoft.Extensions.DependencyInjection;
            using Tsikhanau.Monolith.Api.Modules;
            using Tsikhanau.Monolith.Infrastructure.Modules;

            namespace Acme.Shop.Modules.InventoryReservationAdjustmentApprovals;

            public sealed class InventoryReservationAdjustmentApprovalsModule : Module
            {
                protected override void AddServices(IServiceCollection services) =>
                    AddSettings<InventoryReservationAdjustmentApprovalsSettings>(services)
                        .AddModuleInfrastructure<
                            IInventoryReservationAdjustmentApprovalsModule,
                            InventoryReservationAdjustmentApprovalsModuleFacade,
                            InventoryReservationAdjustmentApprovalsDbContext>();
            }

            """);
    }

    [Theory]
    [InlineData("Notes", false)]
    [InlineData("InventoryReservations", false)]
    [InlineData(LongName, false)]
    [InlineData(MaxName, false)]
    [InlineData(LongName, true)]
    [InlineData(MaxName, true)]
    public void Every_generated_line_fits_in_120_characters(String name, Boolean hasSettings)
    {
        var module = SampleModules.Create(name);

        var longLines = new ModuleTemplate(module, hasSettings)
            .CreateFiles()
            .Where(newFile => newFile.Path.EndsWith(".cs", StringComparison.Ordinal))
            .SelectMany(newFile => newFile.Content.Split('\n'))
            .Where(line => line.Length > 120);

        longLines.ShouldBeEmpty();
    }

    [Fact]
    public void Usings_of_an_app_that_sorts_after_the_core_come_after_the_core_usings()
    {
        var notes = SampleModules.Create("Notes", appName: "Zeta");

        var moduleClass = new ModuleTemplate(notes, hasSettings: false)
            .CreateFiles()
            .Single(newFile => newFile.Path.EndsWith("/NotesModule.cs", StringComparison.Ordinal))
            .Content;

        moduleClass.ShouldStartWith(
            """
            using Microsoft.Extensions.DependencyInjection;
            using Tsikhanau.Monolith.Api.Modules;
            using Tsikhanau.Monolith.Infrastructure.Modules;
            using Zeta.Modules.Notes.Infrastructure;
            using Zeta.Modules.Notes.Infrastructure.Persistence;

            namespace Zeta.Modules.Notes;
            """);
    }
}
