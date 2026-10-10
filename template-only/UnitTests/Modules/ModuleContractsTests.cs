using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Api.Modules;
using Tsikhanau.Monolith.Infrastructure.EventBus;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Monolith.Shared.Reflection;

namespace Tsikhanau.Monolith.UnitTests.Modules;

public sealed class ModuleContractsTests
{
    private const String HushedEventName = "quiet.hushed";

    private const String ContractsSource = """
        using System;
        using Tsikhanau.Monolith.Contracts.IntegrationEvents;

        namespace Acme.Modules.Quiet.Contracts.IntegrationEvents;

        [IntegrationEventName("quiet.hushed")]
        public sealed record HushedIntegrationEvent(DateTimeOffset OccurredOn) : IntegrationEvent(OccurredOn);
        """;

    private const String ModuleSource = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Acme.Modules.Quiet.Contracts.IntegrationEvents;
        using Microsoft.EntityFrameworkCore;
        using Microsoft.Extensions.DependencyInjection;
        using Tsikhanau.Monolith.Api.Modules;
        using Tsikhanau.Monolith.Application.Commands;
        using Tsikhanau.Monolith.Application.Events;
        using Tsikhanau.Monolith.Application.Modules;
        using Tsikhanau.Monolith.Infrastructure.Dispatching;
        using Tsikhanau.Monolith.Infrastructure.Modules;
        using Tsikhanau.Monolith.Infrastructure.Persistence;
        using Tsikhanau.Railway;

        namespace Acme.Modules.Quiet
        {
            public sealed class QuietModule : Module
            {
                protected override void AddServices(IServiceCollection services) =>
                    services.AddModuleInfrastructure<IQuietModule, QuietModuleFacade, QuietDbContext>();
            }

            internal interface IQuietModule : IModuleFacade;

            internal sealed class QuietModuleFacade(IServiceScopeFactory serviceScopeFactory)
                : ModuleFacade<IQuietModule>(serviceScopeFactory), IQuietModule;

            internal sealed class QuietDbContext(DbContextOptions<QuietDbContext> dbContextOptions)
                : ModuleDbContext(dbContextOptions)
            {
                public const String SchemaName = "quiet";
            }
        }

        namespace Acme.Modules.Quiet.Features.Hush
        {
            internal sealed record HushCommand : ICommand<Unit>;

            internal sealed class HushCommandHandler(IOutbox<IQuietModule> outbox, TimeProvider timeProvider)
                : ICommandHandler<HushCommand, Unit>
            {
                public Task<Result<Unit>> Handle(HushCommand command, CancellationToken cancellationToken)
                {
                    outbox.Add(new HushedIntegrationEvent(timeProvider.GetUtcNow()));
                    return Task.FromResult(Result.Success());
                }
            }
        }
        """;

    private static readonly Lazy<QuietModuleTypes> QuietTypes = new(CompileQuietModule);

    [Fact]
    public void Integration_events_in_the_contracts_assembly_of_a_module_without_a_query_facade_are_registered()
    {
        var services = new ServiceCollection();
        var quietModule = QuietTypes.Value.ModuleClassType.CreateInstance<IModule>();

        quietModule.Register(services, EmptySettingsSection);

        var foundHushedEventType = services
            .GetOrAddInstance<IntegrationEventRegistry>()
            .FindType(HushedEventName);

        foundHushedEventType.ShouldBe(QuietTypes.Value.HushedIntegrationEventType);
    }

    private static IConfigurationSection EmptySettingsSection =>
        new ConfigurationBuilder()
            .Build()
            .GetSection("App:Modules:Quiet");

    private static IEnumerable<MetadataReference> RuntimeReferences =>
        ((String)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));

    private static QuietModuleTypes CompileQuietModule()
    {
        IReadOnlyList<MetadataReference> runtimeReferences = [.. RuntimeReferences];
        var contractsImage = Compile("Acme.Modules.Quiet.Contracts", ContractsSource, runtimeReferences);
        var moduleImage = Compile(
            "Acme.Modules.Quiet",
            ModuleSource,
            [.. runtimeReferences, MetadataReference.CreateFromImage(contractsImage)]);

        var contractsAssembly = LoadIntoDefaultContext(contractsImage);
        var moduleAssembly = LoadIntoDefaultContext(moduleImage);

        return new QuietModuleTypes(GetOnlyPublicType(moduleAssembly), GetOnlyPublicType(contractsAssembly));
    }

    private static Byte[] Compile(String assemblyName, String source, IEnumerable<MetadataReference> references)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var imageStream = new MemoryStream();
        var emitResult = compilation.Emit(imageStream);

        emitResult.Diagnostics
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ShouldBeEmpty($"{assemblyName} does not compile");

        return imageStream.ToArray();
    }

    private static Assembly LoadIntoDefaultContext(Byte[] image)
    {
        using var imageStream = new MemoryStream(image);
        return AssemblyLoadContext.Default.LoadFromStream(imageStream);
    }

    private static Type GetOnlyPublicType(Assembly assembly) =>
        assembly
            .GetExportedTypes()
            .Single();

    private sealed record QuietModuleTypes(Type ModuleClassType, Type HushedIntegrationEventType);
}
