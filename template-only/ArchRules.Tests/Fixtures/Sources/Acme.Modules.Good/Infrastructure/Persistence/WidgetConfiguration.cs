using Acme.Modules.Good.Domain.Widgets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acme.Modules.Good.Infrastructure.Persistence;

internal sealed class WidgetConfiguration : IEntityTypeConfiguration<Widget>
{
    public void Configure(EntityTypeBuilder<Widget> builder)
    {
        builder.ToTable("widgets");
        builder.HasKey(widget => widget.Id);
    }
}
