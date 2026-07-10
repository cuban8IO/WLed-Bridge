namespace wledBridge.Application.Abstractions.Midi;

public interface IMidiInputPort : IDisposable
{
    string DeviceName { get; }

    event EventHandler<MidiMessage>? MessageReceived;

    event EventHandler<byte[]>? SysExReceived;
}
