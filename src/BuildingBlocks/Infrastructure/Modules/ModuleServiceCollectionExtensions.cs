using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ModularMonolith.BuildingBlocks.Application.Commands;
using ModularMonolith.BuildingBlocks.Application.Events;
using ModularMonolith.BuildingBlocks.Application.Modules;
using ModularMonolith.BuildingBlocks.Application.Queries;
using ModularMonolith.BuildingBlocks.Contracts.IntegrationEvents;
using ModularMonolith.BuildingBlocks.Contracts.Modules;
using ModularMonolith.BuildingBlocks.Infrastructure.Cleanup;
using ModularMonolith.BuildingBlocks.Infrastructure.Database;
using ModularMonolith.BuildingBlocks.Infrastructure.Dispatching;
using ModularMonolith.BuildingBlocks.Infrastructure.EventBus;
using ModularMonolith.BuildingBlocks.Infrastructure.Inbox;
using ModularMonolith.BuildingBlocks.Infrastructure.InternalCommands;
using ModularMonolith.BuildingBlocks.Infrastructure.Outbox;
using ModularMonolith.BuildingBlocks.Infrastructure.Persistence;
using ModularMonolith.BuildingBlocks.Infrastructure.Queues;
using ModularMonolith.Shared.Collections;
using ModularMonolith.Shared.Reflection;
using Npgsql;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Modules;

public static class ModuleServiceCollectionExtensions
{
    public static IServiceCollection AddModuleInfrastructure<TModule, TQueryFacade, TFacade, TDbContext>(
        this IServiceCollection services,
        ModuleDefinition definition)
        where TModule : class, IModuleFacade, TQueryFacade
        where TQueryFacade : class, IModuleQueryFacade
        where TFacade : class, TModule
        where TDbContext : ModuleDbContext
    {
        var moduleTypes = GetModuleTypes<TQueryFacade>(definition);
        EnsureHandlersAreValid(definition, moduleTypes);
        EnsureInternalCommandsAreNamed(definition, moduleTypes);
        EnsureConstraintNamesFit(definition);
        var descriptor = Describe<TModule, TQueryFacade, TDbContext>(definition);

        return services
            .AddDescriptor(descriptor)
            .AddPersistence<TModule, TDbContext>(descriptor)
            .AddDispatching<TModule, TQueryFacade, TFacade>()
            .AddOutbox<TModule>()
            .AddInbox<TModule>()
            .AddInternalCommands<TModule>(moduleTypes)
            .AddQueueProcessing<TModule>()
            .AddCleanup<TModule>()
            .AddHandlers(moduleTypes)
            .AddIntegrationEvents(moduleTypes)
            .AddIntegrationEventHandlers(moduleTypes, descriptor);
    }

    private static List<Type> GetModuleTypes<TQueryFacade>(ModuleDefinition definition) =>
        GetModuleAssemblies<TQueryFacade>(definition)
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsInNamespace(definition.RootNamespace))
            .ToList();

    private static IEnumerable<Assembly> GetModuleAssemblies<TQueryFacade>(ModuleDefinition definition) =>
        new[] { definition.Assembly, typeof(TQueryFacade).Assembly }.Distinct();

    private static void EnsureHandlersAreValid(ModuleDefinition definition, IReadOnlyCollection<Type> moduleTypes) =>
        EnsureNoProblems(
            definition,
            "handlers",
            HandlerRegistrationValidator.CollectProblems(moduleTypes, definition.RootNamespace));

    private static void EnsureInternalCommandsAreNamed(
        ModuleDefinition definition,
        IReadOnlyCollection<Type> moduleTypes) =>
        EnsureNoProblems(
            definition,
            "internal command names",
            InternalCommandNameValidator.CollectProblems(moduleTypes));

    private static void EnsureConstraintNamesFit(ModuleDefinition definition) =>
        EnsureNoProblems(
            definition,
            "constraint names",
            ConstraintNameValidator.CollectProblems(definition.ConstraintErrors.Keys));

    private static void EnsureNoProblems(ModuleDefinition definition, String subject, IReadOnlyList<String> problems)
    {
        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                $"Module {definition.Name} has invalid {subject}: {String.Join("; ", problems)}");
        }
    }

    private static ModuleDescriptor Describe<TModule, TQueryFacade, TDbContext>(ModuleDefinition definition) =>
        new(
            typeof(TModule),
            typeof(TQueryFacade),
            definition.Name,
            definition.Schema,
            typeof(TDbContext),
            definition.ConstraintErrors);

    private static IServiceCollection AddDescriptor(this IServiceCollection services, ModuleDescriptor descriptor)
    {
        services
            .GetOrAddInstance<ModuleRegistry>()
            .Add(descriptor);

        return services;
    }

    private static IServiceCollection AddPersistence<TModule, TDbContext>(
        this IServiceCollection services,
        ModuleDescriptor descriptor)
        where TModule : class, IModuleFacade
        where TDbContext : ModuleDbContext =>
        services
            .AddKeyedSingleton(
                descriptor.Key,
                (serviceProvider, _) => ModuleDataSourceFactory.Create(serviceProvider, descriptor))
            .AddDbContext<TDbContext>(
                (serviceProvider, dbContextOptionsBuilder) =>
                    ConfigureDbContext(serviceProvider, dbContextOptionsBuilder, descriptor))
            .AddScoped(
                serviceProvider =>
                    new ModuleContext<TModule>(serviceProvider.GetRequiredService<TDbContext>(), descriptor))
            .AddScoped<IUnitOfWork<TModule>, UnitOfWork<TModule>>()
            .AddSingleton<ISqlConnectionFactory<TModule>>(
                serviceProvider => new SqlConnectionFactory<TModule>(ResolveDataSource(serviceProvider, descriptor)))
            .AddDatabaseHealth(descriptor);

    private static NpgsqlDataSource ResolveDataSource(IServiceProvider serviceProvider, ModuleDescriptor descriptor) =>
        serviceProvider.GetRequiredKeyedService<NpgsqlDataSource>(descriptor.Key);

    private static void ConfigureDbContext(
        IServiceProvider serviceProvider,
        DbContextOptionsBuilder dbContextOptionsBuilder,
        ModuleDescriptor descriptor) =>
        ModuleDbContextOptions.Configure(
            dbContextOptionsBuilder,
            ResolveDataSource(serviceProvider, descriptor),
            descriptor.Schema);

    private static IServiceCollection AddDatabaseHealth(
        this IServiceCollection services,
        ModuleDescriptor descriptor) =>
        services
            .AddHealthChecks()
            .Add(CreateDatabaseHealthCheckRegistration(descriptor))
            .Services;

    private static HealthCheckRegistration CreateDatabaseHealthCheckRegistration(ModuleDescriptor descriptor) =>
        new(
            $"database-{descriptor.Schema}",
            serviceProvider => new ModuleDatabaseHealthCheck(ResolveDataSource(serviceProvider, descriptor)),
            failureStatus: null,
            tags: null);

    private static IServiceCollection AddDispatching<TModule, TQueryFacade, TFacade>(this IServiceCollection services)
        where TModule : class, IModuleFacade, TQueryFacade
        where TQueryFacade : class, IModuleQueryFacade
        where TFacade : class, TModule =>
        services
            .AddSingleton<TModule, TFacade>()
            .AddSingleton<TQueryFacade>(serviceProvider => serviceProvider.GetRequiredService<TModule>())
            .AddScoped<ModuleDispatcher<TModule>>();

    private static IServiceCollection AddOutbox<TModule>(this IServiceCollection services)
        where TModule : class, IModuleFacade =>
        services
            .AddScoped<IOutbox<TModule>, ModuleOutbox<TModule>>()
            .AddSingleton<OutboxPump<TModule>>();

    private static IServiceCollection AddInbox<TModule>(this IServiceCollection services)
        where TModule : class, IModuleFacade =>
        services.AddSingleton<InboxPump<TModule>>();

    private static IServiceCollection AddInternalCommands<TModule>(
        this IServiceCollection services,
        IEnumerable<Type> moduleTypes)
        where TModule : class, IModuleFacade =>
        services
            .AddScoped<IInternalCommandScheduler<TModule>, InternalCommandScheduler<TModule>>()
            .AddSingleton(new InternalCommandRegistry<TModule>(moduleTypes.Implementing<InternalCommand>()))
            .AddSingleton<InternalCommandsPump<TModule>>();

    private static IServiceCollection AddQueueProcessing<TModule>(this IServiceCollection services)
        where TModule : class, IModuleFacade =>
        services
            .AddSingleton<IQueuePump<TModule>, ModuleQueuePump<TModule>>()
            .AddSingleton<IQueuePump>(serviceProvider => serviceProvider.GetRequiredService<IQueuePump<TModule>>())
            .AddHostedService<ModuleQueueProcessor<TModule>>();

    private static IServiceCollection AddCleanup<TModule>(this IServiceCollection services)
        where TModule : class, IModuleFacade =>
        services
            .AddScoped<ICleanupTask<TModule>, ProcessedMessagesCleanupTask<TModule>>()
            .AddSingleton<ModuleCleanup<TModule>>()
            .AddSingleton<IModuleCleanup>(
                serviceProvider => serviceProvider.GetRequiredService<ModuleCleanup<TModule>>())
            .AddHostedService<ModuleCleanupProcessor<TModule>>();

    private static IServiceCollection AddHandlers(this IServiceCollection services, IEnumerable<Type> moduleTypes) =>
        services
            .AddImplementations(moduleTypes, typeof(ICommandHandler<,>))
            .AddImplementations(moduleTypes, typeof(IQueryHandler<,>))
            .AddImplementations(moduleTypes, typeof(IDomainEventHandler<>));

    private static IServiceCollection AddImplementations(
        this IServiceCollection services,
        IEnumerable<Type> moduleTypes,
        Type openGeneric)
    {
        GetImplementations(moduleTypes, openGeneric)
            .ForEach(implementationAndService =>
                services.AddScoped(implementationAndService.ServiceType, implementationAndService.ImplementationType));

        return services;
    }

    private static IServiceCollection AddIntegrationEvents(
        this IServiceCollection services,
        IEnumerable<Type> moduleTypes)
    {
        var integrationEventRegistry = services.GetOrAddInstance<IntegrationEventRegistry>();
        moduleTypes
            .Implementing<IntegrationEvent>()
            .ForEach(integrationEventRegistry.Register);

        return services;
    }

    private static IServiceCollection AddIntegrationEventHandlers(
        this IServiceCollection services,
        IEnumerable<Type> moduleTypes,
        ModuleDescriptor descriptor)
    {
        var integrationEventRegistry = services.GetOrAddInstance<IntegrationEventRegistry>();
        var subscriptionRegistry = services.GetOrAddInstance<SubscriptionRegistry>();
        foreach (var (implementationType, serviceType)
            in GetImplementations(moduleTypes, typeof(IIntegrationEventHandler<>)))
        {
            var eventType = serviceType.GetGenericArguments()[0];
            services.AddKeyedScoped(serviceType, descriptor.Key, implementationType);
            integrationEventRegistry.Register(eventType);
            subscriptionRegistry.Subscribe(
                integrationEventRegistry.GetName(eventType),
                descriptor,
                runsOutsideTransaction: implementationType.Implements<IRunOutsideTransaction>());
        }

        return services;
    }

    private static IEnumerable<(Type ImplementationType, Type ServiceType)> GetImplementations(
        IEnumerable<Type> types,
        Type openGeneric) =>
        types
            .Where(type => type.IsConcrete)
            .SelectMany(type => GetImplementedServices(type, openGeneric));

    private static IEnumerable<(Type ImplementationType, Type ServiceType)> GetImplementedServices(
        Type implementationType,
        Type openGeneric) =>
        implementationType
            .GetInterfacesBuiltFrom(openGeneric)
            .Select(serviceType => (implementationType, serviceType));
}
