using wledBridge.Domain.Midi;

namespace wledBridge.Application.Abstractions;

public interface IMidiInputService
{
    IReadOnlyList<string> GetAvailableDevices();

    void StartListening(string deviceName);

    void StopListening();

    event EventHandler<MidiTrigger>? TriggerReceived;
}
