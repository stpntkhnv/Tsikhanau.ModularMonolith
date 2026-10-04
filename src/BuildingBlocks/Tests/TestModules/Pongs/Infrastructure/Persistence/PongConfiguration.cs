using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModularMonolith.TestModules.Pongs.Domain.Pongs;

namespace ModularMonolith.TestModules.Pongs.Infrastructure.Persistence;

internal sealed class PongConfiguration : IEntityTypeConfiguration<Pong>
{
    public void Configure(EntityTypeBuilder<Pong> builder)
    {
        builder.ToTable("pongs");
        builder.HasKey(pong => pong.Id);

        builder
            .Property(pong => pong.Label)
            .HasMaxLength(Pong.MaxLabelLength);
    }
}
