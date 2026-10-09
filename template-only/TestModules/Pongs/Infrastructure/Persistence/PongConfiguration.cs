using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsikhanau.Monolith.TestModules.Pongs.Domain.Pongs;

namespace Tsikhanau.Monolith.TestModules.Pongs.Infrastructure.Persistence;

internal sealed class PongConfiguration : IEntityTypeConfiguration<Pong>
{
    public void Configure(EntityTypeBuilder<Pong> builder)
    {
        builder.ToTable("pongs");
        builder.HasKey(pong => pong.Id);

        builder
            .Property(pong => pong.Label)
            .HasMaxLength(PongLabel.MaxLength);
    }
}
