namespace wledBridge.Application.Abstractions.Midi;

public interface IMidiPortFactory
{
    IReadOnlyList<string> GetInputDeviceNames();

    IReadOnlyList<string> GetOutputDeviceNames();

    IMidiInputPort OpenInput(string deviceName);

    IMidiOutputPort OpenOutput(string deviceName);
}
