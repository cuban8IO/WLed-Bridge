namespace wledBridge.VirtualMixer.Models;

/// <summary>Lightweight, read-only mixer listing entry.</summary>
public sealed record MixerInfo(Guid Id, string Name, string? Description, bool IsActive, int ControlCount);
