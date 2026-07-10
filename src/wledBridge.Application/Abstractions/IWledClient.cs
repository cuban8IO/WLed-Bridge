using wledBridge.Domain.Wled;

namespace wledBridge.Application.Abstractions;

public interface IWledClient
{
    Task<WledState> GetStateAsync(WledDevice device, CancellationToken cancellationToken = default);

    Task SetStateAsync(WledDevice device, WledState state, CancellationToken cancellationToken = default);
}
