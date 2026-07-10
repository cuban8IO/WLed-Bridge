using System.Net.Http.Json;
using wledBridge.Application.Abstractions;
using wledBridge.Domain.Wled;

namespace wledBridge.Infrastructure.Wled;

public class WledHttpClient(HttpClient httpClient) : IWledClient
{
    public async Task<WledState> GetStateAsync(WledDevice device, CancellationToken cancellationToken = default)
    {
        var dto = await httpClient.GetFromJsonAsync<WledStateDto>($"http://{device.Host}/json/state", cancellationToken)
            ?? throw new InvalidOperationException($"WLED device '{device.Name}' returned no state.");

        var color = dto.Seg?.FirstOrDefault()?.Col?.FirstOrDefault();

        return new WledState(
            dto.On,
            dto.Bri,
            color?.ElementAtOrDefault(0) ?? 0,
            color?.ElementAtOrDefault(1) ?? 0,
            color?.ElementAtOrDefault(2) ?? 0);
    }

    public async Task SetStateAsync(WledDevice device, WledState state, CancellationToken cancellationToken = default)
    {
        var dto = new WledStateDto
        {
            On = state.On,
            Bri = state.Brightness,
            Seg = [new WledSegmentDto { Col = [[state.Red, state.Green, state.Blue]] }]
        };

        var response = await httpClient.PostAsJsonAsync($"http://{device.Host}/json/state", dto, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
