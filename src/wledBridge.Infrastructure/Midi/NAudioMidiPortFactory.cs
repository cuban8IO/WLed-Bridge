using NAudio.Midi;
using wledBridge.Application.Abstractions.Midi;

namespace wledBridge.Infrastructure.Midi;

public class NAudioMidiPortFactory : IMidiPortFactory
{
    public IReadOnlyList<string> GetInputDeviceNames()
    {
        var devices = new List<string>();
        for (var i = 0; i < MidiIn.NumberOfDevices; i++)
        {
            devices.Add(MidiIn.DeviceInfo(i).ProductName);
        }

        return devices;
    }

    public IReadOnlyList<string> GetOutputDeviceNames()
    {
        var devices = new List<string>();
        for (var i = 0; i < MidiOut.NumberOfDevices; i++)
        {
            devices.Add(MidiOut.DeviceInfo(i).ProductName);
        }

        return devices;
    }

    public IMidiInputPort OpenInput(string deviceName)
    {
        var deviceNumber = FindInputDevice(deviceName)
            ?? throw new InvalidOperationException($"MIDI input device '{deviceName}' was not found.");

        return new NAudioMidiInputPort(deviceNumber, deviceName);
    }

    public IMidiOutputPort OpenOutput(string deviceName)
    {
        var deviceNumber = FindOutputDevice(deviceName)
            ?? throw new InvalidOperationException($"MIDI output device '{deviceName}' was not found.");

        return new NAudioMidiOutputPort(deviceNumber, deviceName);
    }

    private static int? FindInputDevice(string deviceName)
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

    private static int? FindOutputDevice(string deviceName)
    {
        for (var i = 0; i < MidiOut.NumberOfDevices; i++)
        {
            if (MidiOut.DeviceInfo(i).ProductName == deviceName)
            {
                return i;
            }
        }

        return null;
    }
}
