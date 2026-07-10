using wledBridge.Application.Abstractions;
using wledBridge.Domain.Midi;

namespace wledBridge.Infrastructure.Midi;

public class NullMidiInputService : IMidiInputService
{
    public event EventHandler<MidiTrigger>? TriggerReceived;

    public IReadOnlyList<string> GetAvailableDevices() => [];

    public void StartListening(string deviceName)
    {
    }

    public void StopListening()
    {
    }
}
