using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using wledBridge.VirtualMixer.Models;
using wledBridge.VirtualMixer.Services;
using wledBridge.VirtualMixer.State;

namespace wledBridge.VirtualMixer.Http;

public static class VirtualMixerApiEndpoints
{
    /// <summary>
    /// Opt-in remote control API: REST commands/queries plus the SignalR hub at
    /// "{prefix}/hub". Requires <c>builder.Services.AddSignalR()</c> in the host.
    /// Securing these endpoints (auth, network scoping) is the host's responsibility -
    /// without this call, neither the endpoints nor the hub exist.
    /// </summary>
    public static IEndpointRouteBuilder MapVirtualMixerApi(
        this IEndpointRouteBuilder endpoints,
        string prefix = "/api/virtualmixer")
    {
        var group = endpoints.MapGroup(prefix);

        group.MapGet("/mixers", async (IVirtualMixerManager manager) =>
        {
            var mixers = await manager.GetMixersAsync();
            return Results.Ok(mixers.Select(m => new MixerInfoDto(m.Id, m.Name, m.Description, m.IsActive, m.ControlCount)));
        });

        group.MapGet("/mixers/active", async (IVirtualMixerManager manager) =>
        {
            var mixer = await manager.GetActiveMixerAsync();
            return mixer is null
                ? Results.NotFound()
                : Results.Ok(new MixerInfoDto(mixer.Id, mixer.Name, mixer.Description, mixer.IsActive, mixer.AllControls.Count()));
        });

        group.MapGet("/controls/{controlId:guid}", (Guid controlId, IVirtualMixerRuntime runtime) =>
        {
            var state = runtime.GetControlState(controlId);
            return state is null
                ? Results.NotFound()
                : Results.Ok(new ControlStateDto(state.ControlId, state.TypeKey, state.Value, state.IsOn,
                    state.LedBrightness, state.LedColorHex, state.SubValues));
        });

        group.MapPost("/controls/{controlId:guid}/value", async (Guid controlId, SetValueRequest request, IVirtualMixerRuntime runtime) =>
        {
            await runtime.SetControlValueAsync(controlId, request.Value, CommandSource.Network);
            return Results.Accepted();
        });

        group.MapPost("/controls/{controlId:guid}/button", async (Guid controlId, SetButtonStateRequest request, IVirtualMixerRuntime runtime) =>
        {
            await runtime.SetButtonStateAsync(controlId, request.IsOn, CommandSource.Network);
            return Results.Accepted();
        });

        group.MapPost("/controls/{controlId:guid}/led", async (Guid controlId, SetLedRequest request, IVirtualMixerRuntime runtime) =>
        {
            await runtime.SetLedAsync(controlId, new LedState(request.Brightness, request.ColorHex), CommandSource.Network);
            return Results.Accepted();
        });

        group.MapPost("/controls/{controlId:guid}/sub/{subIndex:int}/value", async (Guid controlId, int subIndex, SetValueRequest request, IVirtualMixerRuntime runtime) =>
        {
            await runtime.SetSubValueAsync(controlId, subIndex, request.Value, CommandSource.Network);
            return Results.Accepted();
        });

        endpoints.MapHub<VirtualMixerHub>($"{prefix}/hub");

        return endpoints;
    }
}
