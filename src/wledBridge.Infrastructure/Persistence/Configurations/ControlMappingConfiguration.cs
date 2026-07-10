using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using wledBridge.Domain.Controllers;

namespace wledBridge.Infrastructure.Persistence.Configurations;

public class ControlMappingConfiguration : IEntityTypeConfiguration<ControlMapping>
{
    public void Configure(EntityTypeBuilder<ControlMapping> builder)
    {
        builder.ToTable("ControlMappings");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PhysicalControlId)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(x => new { x.ControllerDeviceId, x.PhysicalControlId })
            .IsUnique();
    }
}
