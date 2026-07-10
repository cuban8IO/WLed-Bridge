using wledBridge.Application.Abstractions.Controllers;
using wledBridge.Application.Abstractions.Midi;
using wledBridge.Domain.Controllers;

namespace wledBridge.Infrastructure.Controllers;

/// <remarks>
/// Control addresses follow the publicly documented Akai APC40 mkII communication protocol
/// (Ableton Live mode). Some secondary buttons are best-effort; use the Device Test Page to
/// verify/correct against real hardware.
/// </remarks>
public class Apc40Mk2Driver : IControllerDriver
{
    public const string Key = "Apc40Mk2";

    // Mode byte: 0x41 = Ableton Live mode (confirmed working - pads, faders, buttons and
    // encoder position feedback all work in this mode). 0x42 (Alternate Ableton Live mode)
    // was tried to see if it changed encoder ring rendering, but it broke ring feedback
    // entirely instead, so it's reverted.
    private static readonly byte[] AbletonLiveModeSysEx = [0xF0, 0x47, 0x7F, 0x29, 0x60, 0x00, 0x04, 0x41, 0x00, 0x00, 0x00, 0xF7];

    private readonly List<ControlEntry> _entries;
    private readonly Dictionary<string, ControlEntry> _byControlId;
    private readonly Dictionary<RawAddress, ControlEntry> _byNoteAddress;
    private readonly Dictionary<RawAddress, ControlEntry> _byCcAddress;
    private readonly Dictionary<string, double> _encoderPositions = [];

    private IMidiOutputPort? _output;

    public Apc40Mk2Driver()
    {
        _entries = BuildLayout();
        _byControlId = _entries.ToDictionary(e => e.Descriptor.ControlId);

        _byNoteAddress = new Dictionary<RawAddress, ControlEntry>();
        _byCcAddress = new Dictionary<RawAddress, ControlEntry>();

        foreach (var entry in _entries)
        {
            if (entry.Descriptor.Type is ControlType.Pad or ControlType.Button)
            {
                _byNoteAddress[new RawAddress(entry.Channel, 0x90, entry.DataNumber)] = entry;
                _byNoteAddress[new RawAddress(entry.Channel, 0x80, entry.DataNumber)] = entry;
            }
            else
            {
                _byCcAddress[new RawAddress(entry.Channel, 0xB0, entry.DataNumber)] = entry;
            }
        }
    }

    public string DriverKey => Key;

    public string DisplayName => "Akai APC40 mkII";

    public IReadOnlyList<ControlDescriptor> ControlLayout => _entries.Select(e => e.Descriptor).ToList();

    public event EventHandler<ControlValueChangedEventArgs>? ControlChanged;

    public void Attach(IMidiOutputPort output)
    {
        _output = output;
        _output.SendSysEx(AbletonLiveModeSysEx);
    }

    public void Detach() => _output = null;

    public void HandleMessage(MidiMessage message)
    {
        var address = new RawAddress(message.Channel, message.CommandCode, message.Data1);

        var lookup = message.CommandCode switch
        {
            0x90 or 0x80 => _byNoteAddress,
            0xB0 => _byCcAddress,
            _ => null
        };

        if (lookup is null || !lookup.TryGetValue(address, out var entry))
        {
            return;
        }

        var descriptor = entry.Descriptor;

        bool? isOn = null;
        double value;

        if (descriptor.Type is ControlType.Pad or ControlType.Button)
        {
            isOn = message.CommandCode == 0x90 && message.Data2 > 0;
            value = isOn.Value ? 1.0 : 0.0;
        }
        else if (descriptor.Type == ControlType.Encoder)
        {
            // Encoders are relative/endless: data2 is a signed step count (1-63 = clockwise,
            // 65-127 = counter-clockwise), not an absolute position, so it's accumulated here.
            var delta = message.Data2 < 64 ? message.Data2 : message.Data2 - 128;
            var current = _encoderPositions.GetValueOrDefault(descriptor.ControlId, 0.5);
            value = Math.Clamp(current + (delta / 127.0), 0.0, 1.0);
            _encoderPositions[descriptor.ControlId] = value;
        }
        else
        {
            value = message.Data2 / 127.0;
        }

        ControlChanged?.Invoke(this, new ControlValueChangedEventArgs(descriptor.ControlId, descriptor.Type, value, isOn));
    }

    public void HandleSysEx(byte[] data)
    {
        // Device inquiry / introduction responses are not currently interpreted.
    }

    public void SetPadColor(string controlId, ControllerColor color)
    {
        if (_output is null || !_byControlId.TryGetValue(controlId, out var entry) || entry.Descriptor.Type != ControlType.Pad)
        {
            return;
        }

        var velocity = Apc40ColorPalette.FindNearestVelocity(color);
        _output.Send(MidiMessage.NoteOn(entry.Channel, entry.DataNumber, velocity));
    }

    public void SetLedRingValue(string controlId, double value, EncoderRingStyle style = EncoderRingStyle.Position)
    {
        if (_output is null || !_byControlId.TryGetValue(controlId, out var entry) || entry.Descriptor.Type != ControlType.Encoder)
        {
            return;
        }

        var clamped = Math.Clamp(value, 0.0, 1.0);
        _encoderPositions[controlId] = clamped;

        var midiValue = (int)Math.Clamp(Math.Round(clamped * 127), 0, 127);

        // EncoderRingStyle.Fill is not achievable on this device via any confirmed protocol
        // detail (channel+1 and the alternate intro mode were both tried against hardware and
        // ruled out), so it currently falls back to the same single-LED position feedback.
        _output.Send(MidiMessage.ControlChange(entry.Channel, entry.DataNumber, midiValue));
    }

    public void SetButtonLed(string controlId, bool isOn)
    {
        if (_output is null || !_byControlId.TryGetValue(controlId, out var entry) || entry.Descriptor.Type != ControlType.Button)
        {
            return;
        }

        _output.Send(MidiMessage.NoteOn(entry.Channel, entry.DataNumber, isOn ? (byte)127 : (byte)0));
    }

    private static List<ControlEntry> BuildLayout()
    {
        var entries = new List<ControlEntry>();

        for (var col = 0; col < 8; col++)
        {
            for (var row = 0; row < 5; row++)
            {
                entries.Add(new ControlEntry(
                    new ControlDescriptor($"pad-{col}-{row}", ControlType.Pad, $"Pad {col + 1}/{row + 1}", row, col, true, false),
                    0,
                    (row * 8) + col));
            }

            entries.Add(new ControlEntry(new ControlDescriptor($"fader-{col}", ControlType.Fader, $"Fader {col + 1}", null, col, false, false), col, 7));
            entries.Add(new ControlEntry(new ControlDescriptor($"track-activator-{col}", ControlType.Button, $"Activator {col + 1}", null, col, false, false), col, 50));
            entries.Add(new ControlEntry(new ControlDescriptor($"track-solo-{col}", ControlType.Button, $"Solo/Cue {col + 1}", null, col, false, false), col, 49));
            entries.Add(new ControlEntry(new ControlDescriptor($"track-recarm-{col}", ControlType.Button, $"Record Arm {col + 1}", null, col, false, false), col, 48));
            entries.Add(new ControlEntry(new ControlDescriptor($"track-select-{col}", ControlType.Button, $"Track Select {col + 1}", null, col, false, false), col, 51));
            entries.Add(new ControlEntry(new ControlDescriptor($"clip-stop-{col}", ControlType.Button, $"Clip Stop {col + 1}", null, col, false, false), col, 52));
            entries.Add(new ControlEntry(new ControlDescriptor($"crossfader-assign-{col}", ControlType.Button, $"Crossfader Assign A/B {col + 1}", null, col, false, true), col, 66));
            entries.Add(new ControlEntry(new ControlDescriptor($"device-encoder-{col}", ControlType.Encoder, $"Device Encoder {col + 1}", 0, col, false, true), 0, 16 + col));
            entries.Add(new ControlEntry(new ControlDescriptor($"pan-encoder-{col}", ControlType.Encoder, $"Pan Encoder {col + 1}", 1, col, false, true), 0, 48 + col));
        }

        entries.Add(new ControlEntry(new ControlDescriptor("fader-master", ControlType.Fader, "Master Fader", null, null, false, false), 0, 14));
        entries.Add(new ControlEntry(new ControlDescriptor("crossfader", ControlType.Fader, "Crossfader", null, null, false, false), 0, 15));
        entries.Add(new ControlEntry(new ControlDescriptor("cue-level", ControlType.Encoder, "Cue Level", null, null, false, false), 0, 47));

        for (var scene = 0; scene < 5; scene++)
        {
            entries.Add(new ControlEntry(new ControlDescriptor($"scene-{scene}", ControlType.Button, $"Scene Launch {scene + 1}", null, null, false, false), 0, 82 + scene));
        }

        entries.Add(new ControlEntry(new ControlDescriptor("play", ControlType.Button, "Play", null, null, false, false), Channel: 0, DataNumber: 91));
        entries.Add(new ControlEntry(new ControlDescriptor("stop", ControlType.Button, "Stop", null, null, false, false), Channel: 0, DataNumber: 92));
        entries.Add(new ControlEntry(new ControlDescriptor("record", ControlType.Button, "Record", null, null, false, false), Channel: 0, DataNumber: 93));
        entries.Add(new ControlEntry(new ControlDescriptor("shift", ControlType.Button, "Shift", null, null, false, false), Channel: 0, DataNumber: 98));

        return entries;
    }

    private sealed record RawAddress(int Channel, int CommandCode, int DataNumber);

    private sealed record ControlEntry(ControlDescriptor Descriptor, int Channel, int DataNumber);
}
