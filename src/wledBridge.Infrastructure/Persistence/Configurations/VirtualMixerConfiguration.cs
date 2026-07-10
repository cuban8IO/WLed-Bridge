using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using wledBridge.Domain.Controllers;

namespace wledBridge.Infrastructure.Persistence.Configurations;

public class VirtualMixerConfiguration : IEntityTypeConfiguration<VirtualMixer>
{
    public void Configure(EntityTypeBuilder<VirtualMixer> builder)
    {
        builder.ToTable("VirtualMixers");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasMany(x => x.Controls)
            .WithOne()
            .HasForeignKey(x => x.VirtualMixerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
