using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Tsikhanau.Monolith.Shared.Reflection;

namespace Tsikhanau.Monolith.Infrastructure.Persistence;

public abstract class ModuleDbContextDesignTimeFactory<TDbContext> : IDesignTimeDbContextFactory<TDbContext>
    where TDbContext : ModuleDbContext
{
    private const String PlaceholderConnectionString = "Host=localhost;Database=placeholder;Username=placeholder";

    private static String Schema => ModuleSchema.Get(typeof(TDbContext));

    private static String ConstructorProblem =>
        $"Module db context {typeof(TDbContext).FullName} must have a constructor taking only {OptionsTypeName}";

    private static String OptionsTypeName => $"DbContextOptions<{typeof(TDbContext).Name}>";

    public TDbContext CreateDbContext(String[] args)
    {
        var dbContextOptionsBuilder = new DbContextOptionsBuilder<TDbContext>();
        ModuleDbContextOptions.Configure(dbContextOptionsBuilder, PlaceholderConnectionString, Schema);
        return CreateDbContext(dbContextOptionsBuilder.Options);
    }

    private static TDbContext CreateDbContext(DbContextOptions<TDbContext> dbContextOptions)
    {
        try
        {
            return typeof(TDbContext).CreateInstance<TDbContext>(dbContextOptions);
        }
        catch (MissingMethodException exception)
        {
            throw new InvalidOperationException(ConstructorProblem, exception);
        }
    }
}
