using NAudio.Midi;
using wledBridge.Application.Abstractions.Midi;
using MidiMessage = wledBridge.Application.Abstractions.Midi.MidiMessage;

namespace wledBridge.Infrastructure.Midi;

public sealed class NAudioMidiInputPort : IMidiInputPort
{
    private readonly MidiIn _midiIn;

    public NAudioMidiInputPort(int deviceNumber, string deviceName)
    {
        DeviceName = deviceName;
        _midiIn = new MidiIn(deviceNumber);
        _midiIn.MessageReceived += OnMessageReceived;
        _midiIn.SysexMessageReceived += OnSysexMessageReceived;
        _midiIn.Start();
    }

    public string DeviceName { get; }

    public event EventHandler<MidiMessage>? MessageReceived;

    public event EventHandler<byte[]>? SysExReceived;

    private void OnMessageReceived(object? sender, MidiInMessageEventArgs e)
    {
        var raw = e.MidiEvent;
        var message = new MidiMessage((int)raw.CommandCode | (raw.Channel - 1), GetData1(raw), GetData2(raw));
        MessageReceived?.Invoke(this, message);
    }

    private void OnSysexMessageReceived(object? sender, MidiInSysexMessageEventArgs e) =>
        SysExReceived?.Invoke(this, e.SysexBytes);

    private static int GetData1(MidiEvent midiEvent) => midiEvent switch
    {
        NoteEvent note => note.NoteNumber,
        ControlChangeEvent cc => (int)cc.Controller,
        PatchChangeEvent patch => patch.Patch,
        _ => 0
    };

    private static int GetData2(MidiEvent midiEvent) => midiEvent switch
    {
        NoteEvent note => note.Velocity,
        ControlChangeEvent cc => cc.ControllerValue,
        _ => 0
    };

    public void Dispose()
    {
        _midiIn.Stop();
        _midiIn.MessageReceived -= OnMessageReceived;
        _midiIn.SysexMessageReceived -= OnSysexMessageReceived;
        _midiIn.Dispose();
    }
}
