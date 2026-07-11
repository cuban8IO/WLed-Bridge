using Microsoft.EntityFrameworkCore;
using wledBridge.Application.Abstractions.Persistence;
using wledBridge.Domain.Controllers;
using wledBridge.Domain.Wled;

namespace wledBridge.Infrastructure.Persistence;

public class WledBridgeDbContext(DbContextOptions<WledBridgeDbContext> options) : DbContext(options), IApplicationDbContext
{
    public DbSet<WledDevice> WledDevices => Set<WledDevice>();

    public DbSet<ControllerDevice> ControllerDevices => Set<ControllerDevice>();

    public DbSet<VirtualMixer> VirtualMixers => Set<VirtualMixer>();

    public DbSet<VirtualControl> VirtualControls => Set<VirtualControl>();

    public DbSet<ControlMapping> ControlMappings => Set<ControlMapping>();

    public DbSet<ControllerControlDefinition> ControllerControlDefinitions => Set<ControllerControlDefinition>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WledBridgeDbContext).Assembly);
    }
}
