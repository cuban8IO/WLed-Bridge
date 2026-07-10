namespace wledBridge.Application.Abstractions.Controllers;

public readonly record struct ControllerColor(byte R, byte G, byte B)
{
    public static readonly ControllerColor Off = new(0, 0, 0);
}
