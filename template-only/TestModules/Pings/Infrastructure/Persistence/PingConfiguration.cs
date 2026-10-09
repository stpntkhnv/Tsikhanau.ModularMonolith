using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsikhanau.Monolith.Infrastructure.Persistence;
using Tsikhanau.Monolith.TestModules.Pings.Domain.Pings;

namespace Tsikhanau.Monolith.TestModules.Pings.Infrastructure.Persistence;

internal sealed class PingConfiguration : IEntityTypeConfiguration<Ping>
{
    public const String LabelIndex = "ux_pings_label";

    public void Configure(EntityTypeBuilder<Ping> builder)
    {
        builder.ToTable("pings");
        builder.HasKey(ping => ping.Id);

        builder
            .Property(ping => ping.Label)
            .HasMaxLength(PingLabel.MaxLength);

        builder
            .HasIndex(ping => ping.Label)
            .IsUnique()
            .HasDatabaseName(LabelIndex);

        builder.HasOptional(ping => ping.ArchivedOn);

        builder
            .HasOptional(ping => ping.Nickname)
            .HasMaxLength(PingLabel.MaxLength);

        builder.ComplexProperty(ping => ping.Schedule, ConfigureSchedule);

        builder.OwnsMany(ping => ping.Echoes, ConfigureEchoes);

        builder
            .Navigation(ping => ping.Echoes)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureSchedule(ComplexPropertyBuilder<PingSchedule> builder)
    {
        builder.Property(schedule => schedule.Interval);
        builder.Property(schedule => schedule.Repeats);
    }

    private static void ConfigureEchoes(OwnedNavigationBuilder<Ping, Echo> builder)
    {
        builder.ToTable("echoes");
        builder
            .WithOwner()
            .HasForeignKey("PingId");

        builder.HasKey(echo => echo.Id);
    }
}
