using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Tsikhanau.Monolith.TestModules.Pings.Domain.Pings;

namespace Tsikhanau.Monolith.TestHost.Probes;

internal sealed class UnmigratedPropertyModelCustomizer(ModelCustomizerDependencies modelCustomizerDependencies)
    : RelationalModelCustomizer(modelCustomizerDependencies)
{
    private const String UnmigratedProperty = "UnmigratedNote";

    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);
        modelBuilder
            .Entity<Ping>()
            .Property<String>(UnmigratedProperty);
    }
}
