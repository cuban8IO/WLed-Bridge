using Microsoft.EntityFrameworkCore;
using wledBridge.Domain.Controllers;
using wledBridge.Domain.Wled;

namespace wledBridge.Application.Abstractions.Persistence;

public interface IApplicationDbContext
{
    DbSet<WledDevice> WledDevices { get; }

    DbSet<ControllerDevice> ControllerDevices { get; }

    DbSet<VirtualMixer> VirtualMixers { get; }

    DbSet<VirtualControl> VirtualControls { get; }

    DbSet<ControlMapping> ControlMappings { get; }

    DbSet<ControllerControlDefinition> ControllerControlDefinitions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
