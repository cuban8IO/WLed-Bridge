using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using wledBridge.Domain.Controllers;

namespace wledBridge.Infrastructure.Persistence.Configurations;

public class VirtualControlConfiguration : IEntityTypeConfiguration<VirtualControl>
{
    public void Configure(EntityTypeBuilder<VirtualControl> builder)
    {
        builder.ToTable("VirtualControls");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.Color)
            .HasMaxLength(20);
    }
}
