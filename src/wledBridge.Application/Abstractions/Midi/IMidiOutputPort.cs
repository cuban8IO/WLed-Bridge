namespace wledBridge.Application.Abstractions.Midi;

public interface IMidiOutputPort : IDisposable
{
    string DeviceName { get; }

    void Send(MidiMessage message);

    void SendSysEx(byte[] data);
}
