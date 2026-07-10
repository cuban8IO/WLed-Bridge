using wledBridge.Application.Abstractions.Controllers;
using wledBridge.Application.Abstractions.Midi;

namespace wledBridge.Application.Controllers;

public sealed class ControllerSession(IControllerDriver driver, IMidiInputPort input, IMidiOutputPort output) : IDisposable
{
    public IControllerDriver Driver { get; } = driver;

    public IMidiInputPort Input { get; } = input;

    public IMidiOutputPort Output { get; } = output;

    public void Dispose()
    {
        Input.MessageReceived -= OnMessageReceived;
        Input.SysExReceived -= OnSysExReceived;
        Driver.Detach();
        Input.Dispose();
        Output.Dispose();
    }

    public void Start()
    {
        Input.MessageReceived += OnMessageReceived;
        Input.SysExReceived += OnSysExReceived;
        Driver.Attach(Output);
    }

    private void OnMessageReceived(object? sender, MidiMessage message) => Driver.HandleMessage(message);

    private void OnSysExReceived(object? sender, byte[] data) => Driver.HandleSysEx(data);
}
