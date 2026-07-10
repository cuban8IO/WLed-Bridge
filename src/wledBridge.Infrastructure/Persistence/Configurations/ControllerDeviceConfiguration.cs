using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using wledBridge.Domain.Controllers;

namespace wledBridge.Infrastructure.Persistence.Configurations;

public class ControllerDeviceConfiguration : IEntityTypeConfiguration<ControllerDevice>
{
    public void Configure(EntityTypeBuilder<ControllerDevice> builder)
    {
        builder.ToTable("ControllerDevices");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.DriverKey)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.MidiInputDeviceName)
            .HasMaxLength(200);

        builder.Property(x => x.MidiOutputDeviceName)
            .HasMaxLength(200);
    }
}
