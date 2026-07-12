namespace wledBridge.VirtualMixer.Models;

/// <summary>
/// Runtime LED command/state: brightness 0..127 plus an optional color override for RGB LEDs.
/// </summary>
public sealed record LedState(int Brightness, string? ColorHex = null);
