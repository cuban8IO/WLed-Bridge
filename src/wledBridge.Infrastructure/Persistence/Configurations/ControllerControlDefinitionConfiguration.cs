using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using wledBridge.Domain.Controllers;

namespace wledBridge.Infrastructure.Persistence.Configurations;

public class ControllerControlDefinitionConfiguration : IEntityTypeConfiguration<ControllerControlDefinition>
{
    public void Configure(EntityTypeBuilder<ControllerControlDefinition> builder)
    {
        builder.ToTable("ControllerControlDefinitions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.LedCapability)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.HasIndex(x => new { x.ControllerDeviceId, x.Channel, x.CommandCode, x.DataNumber })
            .IsUnique();
    }
}
