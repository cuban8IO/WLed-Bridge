using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using wledBridge.Domain.Wled;

namespace wledBridge.Infrastructure.Persistence.Configurations;

public class WledDeviceConfiguration : IEntityTypeConfiguration<WledDevice>
{
    public void Configure(EntityTypeBuilder<WledDevice> builder)
    {
        builder.ToTable("WledDevices");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Host)
            .IsRequired()
            .HasMaxLength(200);
    }
}
