using System.Reflection;
using Acme.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Tsikhanau.Monolith.Infrastructure.Persistence;

namespace Acme.Modules.Good.Infrastructure.Persistence;

internal sealed class GoodDbContext(DbContextOptions<GoodDbContext> dbContextOptions)
    : ModuleDbContext(dbContextOptions)
{
    public const String SchemaName = "good";

    protected override String Schema => SchemaName;

    protected override IReadOnlyList<Assembly> SharedKernelAssemblies => [SharedKernelDomain.Assembly];

    protected override void ConfigureModule(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfiguration(new WidgetConfiguration());
}
