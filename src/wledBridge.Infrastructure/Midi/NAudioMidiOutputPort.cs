using NAudio.Midi;
using wledBridge.Application.Abstractions.Midi;
using MidiMessage = wledBridge.Application.Abstractions.Midi.MidiMessage;

namespace wledBridge.Infrastructure.Midi;

public sealed class NAudioMidiOutputPort : IMidiOutputPort
{
    private readonly MidiOut _midiOut;

    public NAudioMidiOutputPort(int deviceNumber, string deviceName)
    {
        DeviceName = deviceName;
        _midiOut = new MidiOut(deviceNumber);
    }

    public string DeviceName { get; }

    public void Send(MidiMessage message)
    {
        var packed = message.Status | (message.Data1 << 8) | (message.Data2 << 16);
        _midiOut.Send(packed);
    }

    public void SendSysEx(byte[] data) => _midiOut.SendBuffer(data);

    public void Dispose() => _midiOut.Dispose();
}
