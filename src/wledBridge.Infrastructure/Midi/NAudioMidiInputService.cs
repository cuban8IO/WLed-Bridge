using NAudio.Midi;
using wledBridge.Application.Abstractions;
using wledBridge.Domain.Midi;

namespace wledBridge.Infrastructure.Midi;

public class NAudioMidiInputService : IMidiInputService, IDisposable
{
    private MidiIn? _midiIn;

    public event EventHandler<MidiTrigger>? TriggerReceived;

    public IReadOnlyList<string> GetAvailableDevices()
    {
        var devices = new List<string>();
        for (var i = 0; i < MidiIn.NumberOfDevices; i++)
        {
            devices.Add(MidiIn.DeviceInfo(i).ProductName);
        }

        return devices;
    }

    public void StartListening(string deviceName)
    {
        StopListening();

        var deviceNumber = FindDeviceNumber(deviceName);
        if (deviceNumber is null)
        {
            return;
        }

        _midiIn = new MidiIn(deviceNumber.Value);
        _midiIn.MessageReceived += OnMessageReceived;
        _midiIn.Start();
    }

    public void StopListening()
    {
        if (_midiIn is null)
        {
            return;
        }

        _midiIn.Stop();
        _midiIn.MessageReceived -= OnMessageReceived;
        _midiIn.Dispose();
        _midiIn = null;
    }

    private void OnMessageReceived(object? sender, MidiInMessageEventArgs e)
    {
        var trigger = e.MidiEvent switch
        {
            NoteOnEvent { Velocity: > 0 } noteOn => new MidiTrigger(noteOn.Channel, MidiTriggerType.NoteOn, noteOn.NoteNumber),
            NoteOnEvent noteOff => new MidiTrigger(noteOff.Channel, MidiTriggerType.NoteOff, noteOff.NoteNumber),
            NoteEvent { CommandCode: MidiCommandCode.NoteOff } noteOff => new MidiTrigger(noteOff.Channel, MidiTriggerType.NoteOff, noteOff.NoteNumber),
            ControlChangeEvent cc => new MidiTrigger(cc.Channel, MidiTriggerType.ControlChange, (int)cc.Controller),
            _ => null
        };

        if (trigger is not null)
        {
            TriggerReceived?.Invoke(this, trigger);
        }
    }

    private static int? FindDeviceNumber(string deviceName)
    {
        for (var i = 0; i < MidiIn.NumberOfDevices; i++)
        {
            if (MidiIn.DeviceInfo(i).ProductName == deviceName)
            {
                return i;
            }
        }

        return null;
    }

    public void Dispose() => StopListening();
}
