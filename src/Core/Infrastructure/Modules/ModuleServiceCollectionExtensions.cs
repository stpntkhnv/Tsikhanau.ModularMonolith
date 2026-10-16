using System.Collections.ObjectModel;
using System.Diagnostics.Metrics;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.Application.Events;
using Tsikhanau.Monolith.Application.Modules;
using Tsikhanau.Monolith.Application.Queries;
using Tsikhanau.Monolith.Contracts.IntegrationEvents;
using Tsikhanau.Monolith.Contracts.Modules;
using Tsikhanau.Monolith.Infrastructure.Cleanup;
using Tsikhanau.Monolith.Infrastructure.Database;
using Tsikhanau.Monolith.Infrastructure.Diagnostics;
using Tsikhanau.Monolith.Infrastructure.Dispatching;
using Tsikhanau.Monolith.Infrastructure.EventBus;
using Tsikhanau.Monolith.Infrastructure.Inbox;
using Tsikhanau.Monolith.Infrastructure.InternalCommands;
using Tsikhanau.Monolith.Infrastructure.Outbox;
using Tsikhanau.Monolith.Infrastructure.Persistence;
using Tsikhanau.Monolith.Infrastructure.Queues;
using Tsikhanau.Monolith.Shared.Collections;
using Tsikhanau.Monolith.Shared.Reflection;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Infrastructure.Modules;

public static class ModuleServiceCollectionExtensions
{
    private const String QueryFacadeProblem = "must extend at most one query facade, like INotesQueryFacade";

    public static IServiceCollection AddModuleInfrastructure<TModule, TFacade, TDbContext>(
        this IServiceCollection services)
        where TModule : class, IModuleFacade
        where TFacade : class, TModule
        where TDbContext : ModuleDbContext =>
        services.AddModuleInfrastructure<TModule, TFacade, TDbContext>(ReadOnlyDictionary<String, Error>.Empty);

    public static IServiceCollection AddModuleInfrastructure<TModule, TFacade, TDbContext>(
        this IServiceCollection services,
        IReadOnlyDictionary<String, Error> constraintErrors)
        where TModule : class, IModuleFacade
        where TFacade : class, TModule
        where TDbContext : ModuleDbContext =>
        services.AddModuleInfrastructure<TModule, TFacade, TDbContext>(
            ModuleDefinition.Create<TModule, TDbContext>(constraintErrors));

    internal static IServiceCollection AddModuleInfrastructure<TModule, TFacade, TDbContext>(
        this IServiceCollection services,
        ModuleDefinition definition)
        where TModule : class, IModuleFacade
        where TFacade : class, TModule
        where TDbContext : ModuleDbContext
    {
        var queryFacadeType = FindQueryFacadeType<TModule>();
        var moduleTypes = GetModuleTypes(definition, queryFacadeType);
        EnsureHandlersAreValid(definition, moduleTypes);
        EnsureInternalCommandsAreNamed(definition, moduleTypes);
        EnsureConstraintNamesFit(definition);
        var descriptor = Describe<TModule, TDbContext>(definition, queryFacadeType);

        return services
            .AddDescriptor(descriptor)
            .AddPersistence<TModule, TDbContext>(descriptor)
            .AddDispatching<TModule, TFacade>(descriptor)
            .AddOutbox<TModule>()
            .AddInbox<TModule>()
            .AddInternalCommands<TModule>(moduleTypes)
            .AddQueueProcessing<TModule>()
            .AddCleanup<TModule>()
            .AddHandlers(moduleTypes)
            .AddIntegrationEvents(moduleTypes)
            .AddIntegrationEventHandlers(moduleTypes, descriptor);
    }

    private static Optional<Type> FindQueryFacadeType<TModule>()
    {
        var queryFacadeTypes = typeof(TModule)
            .GetInterfaces()
            .Where(IsQueryFacade)
            .ToList();

        return queryFacadeTypes switch
        {
            [] => Optional<Type>.None(),
            [var queryFacadeType] => Optional<Type>.Some(queryFacadeType),
            _ => throw new InvalidOperationException($"Module facade {typeof(TModule).FullName} {QueryFacadeProblem}"),
        };
    }

    private static Boolean IsQueryFacade(Type type) =>
        type != typeof(IModuleQueryFacade) && type.Implements<IModuleQueryFacade>();

    private static List<Type> GetModuleTypes(ModuleDefinition definition, Optional<Type> queryFacadeType) =>
        GetModuleAssemblies(definition, queryFacadeType)
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsInNamespace(definition.RootNamespace))
            .ToList();

    private static IEnumerable<Assembly> GetModuleAssemblies(
        ModuleDefinition definition,
        Optional<Type> queryFacadeType) =>
        LoadOwnReferencedAssemblies(definition)
            .Prepend(definition.Assembly)
            .Concat(GetAssemblyOf(queryFacadeType))
            .Distinct();

    private static IEnumerable<Assembly> LoadOwnReferencedAssemblies(ModuleDefinition definition) =>
        definition.Assembly.GetName().Name is { } moduleAssemblyName
            ? definition.Assembly
                .GetReferencedAssemblies()
                .Where(assemblyName => IsNamedUnder(assemblyName, moduleAssemblyName))
                .Select(Assembly.Load)
            : [];

    private static Boolean IsNamedUnder(AssemblyName assemblyName, String moduleAssemblyName) =>
        assemblyName.Name is { } name && name.StartsWith($"{moduleAssemblyName}.", StringComparison.Ordinal);

    private static Assembly[] GetAssemblyOf(Optional<Type> queryFacadeType) =>
        queryFacadeType.Match<Type, Assembly[]>(type => [type.Assembly], () => []);

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

    private static ModuleDescriptor Describe<TModule, TDbContext>(
        ModuleDefinition definition,
        Optional<Type> queryFacadeType) =>
        new(
            typeof(TModule),
            queryFacadeType,
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
            .AddDbContextPool<TDbContext>(
                (serviceProvider, dbContextOptionsBuilder) =>
                    ConfigureDbContext(serviceProvider, dbContextOptionsBuilder, descriptor))
            .AddScoped(
                serviceProvider =>
                    new ModuleContext<TModule>(serviceProvider.GetRequiredService<TDbContext>(), descriptor))
            .AddScoped<IUnitOfWork<TModule>, UnitOfWork<TModule>>()
            .AddSingleton<ISqlConnectionFactory<TModule>>(
                serviceProvider => new SqlConnectionFactory<TModule>(ResolveDataSource(serviceProvider, descriptor)))
            .AddKeyedSingleton(
                descriptor.Key,
                (serviceProvider, _) => new ModuleDatabaseHealthCheck(ResolveDataSource(serviceProvider, descriptor)))
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
            serviceProvider => serviceProvider.GetRequiredKeyedService<ModuleDatabaseHealthCheck>(descriptor.Key),
            failureStatus: null,
            tags: null,
            timeout: ModuleDatabaseHealthCheck.CheckTimeout);

    private static IServiceCollection AddDispatching<TModule, TFacade>(
        this IServiceCollection services,
        ModuleDescriptor descriptor)
        where TModule : class, IModuleFacade
        where TFacade : class, TModule =>
        services
            .AddSingleton<TModule, TFacade>()
            .AddQueryFacade<TModule>(descriptor.QueryFacadeKey)
            .AddScoped<ModuleDispatcher<TModule>>();

    private static IServiceCollection AddQueryFacade<TModule>(
        this IServiceCollection services,
        Optional<Type> queryFacadeKey)
        where TModule : class, IModuleFacade =>
        queryFacadeKey.Match(
            queryFacadeType => services.AddSingleton(queryFacadeType, ResolveModuleFacade<TModule>),
            () => services);

    private static TModule ResolveModuleFacade<TModule>(IServiceProvider serviceProvider)
        where TModule : class, IModuleFacade =>
        serviceProvider.GetRequiredService<TModule>();

    private static IServiceCollection AddOutbox<TModule>(this IServiceCollection services)
        where TModule : class, IModuleFacade =>
        services
            .AddScoped<IOutbox<TModule>, ModuleOutbox<TModule>>()
            .AddSingleton<OutboxPump<TModule>>()
            .AddSecretRowRule<OutboxPump<TModule>>();

    private static IServiceCollection AddInbox<TModule>(this IServiceCollection services)
        where TModule : class, IModuleFacade =>
        services
            .AddSingleton<InboxPump<TModule>>()
            .AddSecretRowRule<InboxPump<TModule>>();

    private static IServiceCollection AddInternalCommands<TModule>(
        this IServiceCollection services,
        IEnumerable<Type> moduleTypes)
        where TModule : class, IModuleFacade =>
        services
            .AddScoped<IInternalCommandScheduler<TModule>, InternalCommandScheduler<TModule>>()
            .AddSingleton(new InternalCommandRegistry<TModule>(moduleTypes.Implementing<InternalCommand>()))
            .AddSingleton<InternalCommandsPump<TModule>>()
            .AddSecretRowRule<InternalCommandsPump<TModule>>();

    private static IServiceCollection AddSecretRowRule<TPump>(this IServiceCollection services)
        where TPump : class, ISecretRowRule =>
        services.AddSingleton<ISecretRowRule>(serviceProvider => serviceProvider.GetRequiredService<TPump>());

    private static IServiceCollection AddQueueProcessing<TModule>(this IServiceCollection services)
        where TModule : class, IModuleFacade =>
        services
            .AddSingleton<IQueuePump<TModule>, ModuleQueuePump<TModule>>()
            .AddSingleton<IQueuePump>(serviceProvider => serviceProvider.GetRequiredService<IQueuePump<TModule>>())
            .AddHostedService<ModuleQueueProcessor<TModule>>()
            .AddQueueSampling();

    private static IServiceCollection AddQueueSampling(this IServiceCollection services)
    {
        services.TryAddSingleton(CreateQueueMetrics);
        services.TryAddSingleton<QueueSampler>();
        return services.AddHostedService(ResolveQueueSampler);
    }

    private static QueueMetrics CreateQueueMetrics(IServiceProvider serviceProvider) =>
        new(serviceProvider.GetRequiredService<IMeterFactory>(), serviceProvider.GetRequiredService<ModuleRegistry>());

    private static QueueSampler ResolveQueueSampler(IServiceProvider serviceProvider) =>
        serviceProvider.GetRequiredService<QueueSampler>();

    private static IServiceCollection AddCleanup<TModule>(this IServiceCollection services)
        where TModule : class, IModuleFacade =>
        services
            .AddScoped<ICleanupTask<TModule>, QueueMessagesCleanupTask<TModule>>()
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
