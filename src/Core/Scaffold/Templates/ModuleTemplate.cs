using Tsikhanau.Monolith.Scaffold.Modules;
using Tsikhanau.Monolith.Scaffold.Plans;
using static Tsikhanau.Monolith.Scaffold.Templates.CodeLayout;

namespace Tsikhanau.Monolith.Scaffold.Templates;

internal sealed class ModuleTemplate(ScaffoldedModule module, Boolean hasSettings)
{
    private const String CoreFolder = @"..\..\..\Core";

    private readonly FacadeTemplate _facadeTemplate = new(module);

    private String Namespace => module.Namespace;

    private String SettingsNamespace => $"{Namespace}.Settings";

    private ModuleProjects Projects => module.Projects;

    public IReadOnlyList<NewFile> CreateFiles() =>
    [
        new(Projects.ContractsProjectPath, ContractsProject),
        new(Projects.ProjectPath, ModuleProject),
        new(Projects.InProject($"{module.ModuleClass}.cs"), ModuleClass),
        _facadeTemplate.FacadeContractFile,
        _facadeTemplate.FacadeClassFile,
        new(Projects.InProject($"Infrastructure/Persistence/{module.DbContext}.cs"), DbContext),
        new(Projects.InProject($"Infrastructure/Persistence/{DesignTimeFactoryName}.cs"), DesignTimeFactory),
        .. SettingsFiles,
    ];

    private IEnumerable<NewFile> SettingsFiles =>
        hasSettings ? [new(Projects.InProject($"Settings/{SettingsName}.cs"), SettingsRecord)] : [];

    private static String ContractsProject =>
        $$"""
        <Project Sdk="Microsoft.NET.Sdk">
          <ItemGroup>
            <ProjectReference Include="{{CoreFolder}}\Contracts\Tsikhanau.Monolith.Contracts.csproj" />
          </ItemGroup>
        </Project>

        """;

    private String ModuleProject =>
        $$"""
        <Project Sdk="Microsoft.NET.Sdk">
          <ItemGroup>
            <InternalsVisibleTo Include="{{module.IntegrationTestsAssembly}}" />
            <InternalsVisibleTo Include="{{module.UnitTestsAssembly}}" />
          </ItemGroup>
          <ItemGroup>
            <ProjectReference Include="{{Projects.OwnContractsInclude}}" />
            <ProjectReference Include="{{CoreFolder}}\Domain\Tsikhanau.Monolith.Domain.csproj" />
            <ProjectReference Include="{{CoreFolder}}\Application\Tsikhanau.Monolith.Application.csproj" />
            <ProjectReference Include="{{CoreFolder}}\Infrastructure\Tsikhanau.Monolith.Infrastructure.csproj" />
            <ProjectReference Include="{{CoreFolder}}\Api\Tsikhanau.Monolith.Api.csproj" />
          </ItemGroup>
          <ItemGroup>
            <PackageReference Include="Microsoft.EntityFrameworkCore.Design" PrivateAssets="all" />
          </ItemGroup>
        </Project>

        """;

    private String ModuleClass =>
        CodeFile(
            Namespace,
            ModuleClassUsings,
            $$"""
            public sealed class {{module.ModuleClass}} : Module
            {
            {{AddServices}}
            }
            """);

    private IReadOnlyList<String> ModuleClassUsings =>
    [
        "Microsoft.Extensions.DependencyInjection",
        module.InfrastructureNamespace,
        module.PersistenceNamespace,
        "Tsikhanau.Monolith.Api.Modules",
        "Tsikhanau.Monolith.Infrastructure.Modules",
        .. SettingsUsings,
    ];

    private IEnumerable<String> SettingsUsings => hasSettings ? [SettingsNamespace] : [];

    private String AddServices => hasSettings ? AddServicesWithSettings : AddServicesWithoutSettings;

    private String AddServicesWithSettings =>
        FirstThatFits(
            $"""
                 protected override void AddServices(IServiceCollection services) =>
                     AddSettings<{SettingsName}>(services)
                         .AddModuleInfrastructure<{InfrastructureTypes(", ")}>();
             """,
            $"""
                 protected override void AddServices(IServiceCollection services) =>
                     AddSettings<{SettingsName}>(services)
                         .AddModuleInfrastructure<
                             {InfrastructureTypes(",\n                ")}>();
             """);

    private String AddServicesWithoutSettings =>
        FirstThatFits(
            $"""
                 protected override void AddServices(IServiceCollection services) =>
                     services.AddModuleInfrastructure<{InfrastructureTypes(", ")}>();
             """,
            $"""
                 protected override void AddServices(IServiceCollection services) =>
                     services.AddModuleInfrastructure<
                         {InfrastructureTypes(",\n            ")}>();
             """);

    private String DbContext =>
        CodeFile(
            module.PersistenceNamespace,
            ["Microsoft.EntityFrameworkCore", "Tsikhanau.Monolith.Infrastructure.Persistence"],
            $$"""
            {{DbContextDeclaration}}
            {
                public const String SchemaName = "{{module.Schema}}";
            }
            """);

    private String DbContextDeclaration =>
        FirstThatFits(
            $"""
             internal sealed class {module.DbContext}(DbContextOptions<{module.DbContext}> dbContextOptions)
                 : ModuleDbContext(dbContextOptions)
             """,
            $"""
             internal sealed class {module.DbContext}(
                 DbContextOptions<{module.DbContext}> dbContextOptions)
                 : ModuleDbContext(dbContextOptions)
             """);

    private String DesignTimeFactory =>
        CodeFile(
            module.PersistenceNamespace,
            ["Tsikhanau.Monolith.Infrastructure.Persistence"],
            DesignTimeFactoryDeclaration);

    private String DesignTimeFactoryDeclaration =>
        FirstThatFits(
            $"internal sealed class {DesignTimeFactoryName} : ModuleDbContextDesignTimeFactory<{module.DbContext}>;",
            $"""
             internal sealed class {DesignTimeFactoryName}
                 : ModuleDbContextDesignTimeFactory<{module.DbContext}>;
             """);

    private String DesignTimeFactoryName => $"{module.DbContext}DesignTimeFactory";

    private String SettingsRecord => CodeFile(SettingsNamespace, [], $"internal sealed record {SettingsName};");

    private String SettingsName => $"{module.Name}Settings";

    private String InfrastructureTypes(String separator) =>
        String.Join(separator, module.FacadeContract, module.FacadeClass, module.DbContext);
}
