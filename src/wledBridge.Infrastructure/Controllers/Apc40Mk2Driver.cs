using wledBridge.Application.Abstractions.Controllers;
using wledBridge.Application.Abstractions.Midi;
using wledBridge.Domain.Controllers;

namespace wledBridge.Infrastructure.Controllers;

/// <remarks>
/// Control addresses and LED behavior follow the official Akai APC40 mkII Communications
/// Protocol v1.2 (Ableton Live Mode / Mode 1), verified against real hardware for the pads,
/// faders and several buttons.
/// </remarks>
public class Apc40Mk2Driver : IControllerDriver
{
    public const string Key = "Apc40Mk2";

    private static readonly byte[] AbletonLiveModeSysEx = [0xF0, 0x47, 0x7F, 0x29, 0x60, 0x00, 0x04, 0x41, 0x00, 0x00, 0x00, 0xF7];
    private static readonly HashSet<string> RelativeEncoderIds = ["cue-level", "tempo-knob"];

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
        Reset();
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
        else if (descriptor.Type == ControlType.Encoder && RelativeEncoderIds.Contains(descriptor.ControlId))
        {
            // Cue Level and Tempo Knob report relative steps (1-63 clockwise, 64-127
            // counter-clockwise) per the protocol's "Type CC2: Relative Controller" spec.
            var delta = message.Data2 < 64 ? message.Data2 : message.Data2 - 128;
            var current = _encoderPositions.GetValueOrDefault(descriptor.ControlId, 0.5);
            value = Math.Clamp(current + (delta / 127.0), 0.0, 1.0);
            _encoderPositions[descriptor.ControlId] = value;
        }
        else
        {
            // Device/Track knobs report an absolute position per the protocol's
            // "Type CC1: Absolute Controller" spec.
            value = message.Data2 / 127.0;
            _encoderPositions[descriptor.ControlId] = value;
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

        if (entry.RingTypeDataNumber is int ringTypeId)
        {
            // Ring Type: 0=off, 1=Single, 2=Volume Style (fill), 3=Pan Style, 4-127=Single.
            var styleValue = style switch
            {
                EncoderRingStyle.Fill => 2,
                EncoderRingStyle.Pan => 3,
                _ => 1
            };
            _output.Send(MidiMessage.ControlChange(entry.Channel, ringTypeId, styleValue));

            // A brief pause avoids a race where the ring-type and value messages arrive too
            // close together for the device firmware to apply the new style reliably
            // (observed most often when switching to Volume Style).
            Thread.Sleep(5);
        }

        var midiValue = (int)Math.Clamp(Math.Round(clamped * 127), 0, 127);
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

    public void Reset()
    {
        if (_output is null)
        {
            return;
        }

        foreach (var entry in _entries)
        {
            switch (entry.Descriptor.Type)
            {
                case ControlType.Pad or ControlType.Button:
                    _output.Send(MidiMessage.NoteOff(entry.Channel, entry.DataNumber));
                    break;
                case ControlType.Encoder when entry.RingTypeDataNumber is int ringTypeId:
                    _output.Send(MidiMessage.ControlChange(entry.Channel, ringTypeId, 0));
                    _encoderPositions[entry.Descriptor.ControlId] = 0.5;
                    break;
            }
        }
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

            entries.Add(new ControlEntry(new ControlDescriptor($"fader-{col}", ControlType.Fader, $"Fader {col + 1}", null, col, false, false), col, 0x07));
            entries.Add(new ControlEntry(new ControlDescriptor($"track-recarm-{col}", ControlType.Button, $"Record Arm {col + 1}", null, col, false, true), col, 0x30));
            entries.Add(new ControlEntry(new ControlDescriptor($"track-solo-{col}", ControlType.Button, $"Solo/Cue {col + 1}", null, col, false, true), col, 0x31));
            entries.Add(new ControlEntry(new ControlDescriptor($"track-activator-{col}", ControlType.Button, $"Activator {col + 1}", null, col, false, true), col, 0x32));
            entries.Add(new ControlEntry(new ControlDescriptor($"track-select-{col}", ControlType.Button, $"Track Select {col + 1}", null, col, false, true), col, 0x33));
            entries.Add(new ControlEntry(new ControlDescriptor($"clip-stop-{col}", ControlType.Button, $"Clip Stop {col + 1}", null, col, false, true), col, 0x34));
            entries.Add(new ControlEntry(new ControlDescriptor($"crossfader-assign-{col}", ControlType.Button, $"Crossfader Assign A/B {col + 1}", null, col, false, true), col, 0x42));

            entries.Add(new ControlEntry(
                new ControlDescriptor($"device-encoder-{col}", ControlType.Encoder, $"Device Encoder {col + 1}", 0, col, false, true),
                0, 0x10 + col, RingTypeDataNumber: 0x18 + col));
            entries.Add(new ControlEntry(
                new ControlDescriptor($"pan-encoder-{col}", ControlType.Encoder, $"Pan Encoder {col + 1}", 1, col, false, true),
                0, 0x30 + col, RingTypeDataNumber: 0x38 + col));
        }

        entries.Add(new ControlEntry(new ControlDescriptor("fader-master", ControlType.Fader, "Master Fader", null, null, false, false), 0, 0x0E));
        entries.Add(new ControlEntry(new ControlDescriptor("crossfader", ControlType.Fader, "Crossfader", null, null, false, false), 0, 0x0F));
        entries.Add(new ControlEntry(new ControlDescriptor("cue-level", ControlType.Encoder, "Cue Level", null, null, false, false), 0, 0x2F));
        entries.Add(new ControlEntry(new ControlDescriptor("tempo-knob", ControlType.Encoder, "Tempo Knob", null, null, false, false), 0, 0x0D));

        for (var scene = 0; scene < 5; scene++)
        {
            entries.Add(new ControlEntry(new ControlDescriptor($"scene-{scene}", ControlType.Button, $"Scene Launch {scene + 1}", null, null, true, false), 0, 0x52 + scene));
        }

        entries.Add(new ControlEntry(new ControlDescriptor("track-select-master", ControlType.Button, "Master", null, null, false, true), 0, 0x50));
        entries.Add(new ControlEntry(new ControlDescriptor("stop-all-clips", ControlType.Button, "Stop All Clips", null, null, false, false), 0, 0x51));
        entries.Add(new ControlEntry(new ControlDescriptor("device-left", ControlType.Button, "Device Left", null, null, false, true), 0, 0x3A));
        entries.Add(new ControlEntry(new ControlDescriptor("device-right", ControlType.Button, "Device Right", null, null, false, true), 0, 0x3B));
        entries.Add(new ControlEntry(new ControlDescriptor("bank-left", ControlType.Button, "Bank Left", null, null, false, true), 0, 0x3C));
        entries.Add(new ControlEntry(new ControlDescriptor("bank-right", ControlType.Button, "Bank Right", null, null, false, true), 0, 0x3D));
        entries.Add(new ControlEntry(new ControlDescriptor("device-onoff", ControlType.Button, "Device On/Off", null, null, false, true), 0, 0x3E));
        entries.Add(new ControlEntry(new ControlDescriptor("device-lock", ControlType.Button, "Device Lock", null, null, false, true), 0, 0x3F));
        entries.Add(new ControlEntry(new ControlDescriptor("clip-device-view", ControlType.Button, "Clip/Device View", null, null, false, true), 0, 0x40));
        entries.Add(new ControlEntry(new ControlDescriptor("detail-view", ControlType.Button, "Detail View", null, null, false, true), 0, 0x41));
        entries.Add(new ControlEntry(new ControlDescriptor("pan-mode", ControlType.Button, "Pan", null, null, false, true), 0, 0x57));
        entries.Add(new ControlEntry(new ControlDescriptor("sends-mode", ControlType.Button, "Sends", null, null, false, true), 0, 0x58));
        entries.Add(new ControlEntry(new ControlDescriptor("user-mode", ControlType.Button, "User", null, null, false, true), 0, 0x59));
        entries.Add(new ControlEntry(new ControlDescriptor("metronome", ControlType.Button, "Metronome", null, null, false, true), 0, 0x5A));
        entries.Add(new ControlEntry(new ControlDescriptor("play", ControlType.Button, "Play", null, null, false, true), 0, 0x5B));
        entries.Add(new ControlEntry(new ControlDescriptor("stop", ControlType.Button, "Stop", null, null, false, false), 0, 0x5C));
        entries.Add(new ControlEntry(new ControlDescriptor("record", ControlType.Button, "Record", null, null, false, true), 0, 0x5D));
        entries.Add(new ControlEntry(new ControlDescriptor("nav-up", ControlType.Button, "Up", null, null, false, false), 0, 0x5E));
        entries.Add(new ControlEntry(new ControlDescriptor("nav-down", ControlType.Button, "Down", null, null, false, false), 0, 0x5F));
        entries.Add(new ControlEntry(new ControlDescriptor("nav-right", ControlType.Button, "Right", null, null, false, false), 0, 0x60));
        entries.Add(new ControlEntry(new ControlDescriptor("nav-left", ControlType.Button, "Left", null, null, false, false), 0, 0x61));
        entries.Add(new ControlEntry(new ControlDescriptor("shift", ControlType.Button, "Shift", null, null, false, false), 0, 0x62));
        entries.Add(new ControlEntry(new ControlDescriptor("tap-tempo", ControlType.Button, "Tap Tempo", null, null, false, false), 0, 0x63));
        entries.Add(new ControlEntry(new ControlDescriptor("nudge-down", ControlType.Button, "Nudge -", null, null, false, false), 0, 0x64));
        entries.Add(new ControlEntry(new ControlDescriptor("nudge-up", ControlType.Button, "Nudge +", null, null, false, false), 0, 0x65));
        entries.Add(new ControlEntry(new ControlDescriptor("session-record", ControlType.Button, "Session Record", null, null, false, true), 0, 0x66));
        entries.Add(new ControlEntry(new ControlDescriptor("bank-lock", ControlType.Button, "Bank Lock", null, null, false, false), 0, 0x67));

        return entries;
    }

    private sealed record RawAddress(int Channel, int CommandCode, int DataNumber);

    private sealed record ControlEntry(ControlDescriptor Descriptor, int Channel, int DataNumber, int? RingTypeDataNumber = null);
}
