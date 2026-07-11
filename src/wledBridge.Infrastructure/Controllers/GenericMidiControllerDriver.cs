using wledBridge.Application.Abstractions.Controllers;
using wledBridge.Application.Abstractions.Midi;
using wledBridge.Domain.Controllers;

namespace wledBridge.Infrastructure.Controllers;

/// <remarks>
/// Drives a controller whose control layout was learned at runtime (see the "Anlernen" workflow
/// on the Device page) rather than hardcoded per model. Input handling (address-based lookup,
/// absolute/relative value decoding) mirrors <see cref="Apc40Mk2Driver"/>. Output/LED feedback is
/// necessarily best-effort here since the actual feedback protocol of an unknown controller can't
/// be inferred just from watching its input messages:
/// - Single LED: Note-on/off (velocity 127/0) on the same address as the input - the most common
///   convention for a simple on/off button LED.
/// - Bar/Rgb: a raw Control Change with the given value on the same address - a common echo
///   convention for lit/motorized faders and rings, but unverified for any specific device.
/// </remarks>
public class GenericMidiControllerDriver(string displayName, IReadOnlyList<GenericControlDefinition> controls) : IControllerDriver
{
    public const string Key = GenericDriverConstants.DriverKey;

    private readonly Dictionary<string, GenericControlDefinition> _byControlId = controls.ToDictionary(c => c.ControlId);
    private readonly Dictionary<RawAddress, GenericControlDefinition> _byNoteAddress = BuildNoteAddressLookup(controls);
    private readonly Dictionary<RawAddress, GenericControlDefinition> _byCcAddress = BuildCcAddressLookup(controls);
    private readonly Dictionary<string, double> _relativePositions = [];

    private IMidiOutputPort? _output;

    public string DriverKey => Key;

    public string DisplayName => displayName;

    public IReadOnlyList<ControlDescriptor> ControlLayout { get; } =
        [.. controls.Select(c => new ControlDescriptor(c.ControlId, c.Type, c.Label, null, null, c.LedCapability == LedCapability.Rgb, c.LedCapability != LedCapability.None))];

    public event EventHandler<ControlValueChangedEventArgs>? ControlChanged;

    public void Attach(IMidiOutputPort output) => _output = output;

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

        if (lookup is null || !lookup.TryGetValue(address, out var definition))
        {
            return;
        }

        bool? isOn = null;
        double value;

        if (definition.Type is ControlType.Pad or ControlType.Button)
        {
            isOn = message.CommandCode == 0x90 && message.Data2 > 0;
            value = isOn.Value ? 1.0 : 0.0;
        }
        else if (definition.IsRelative)
        {
            var delta = message.Data2 < 64 ? message.Data2 : message.Data2 - 128;
            var current = _relativePositions.GetValueOrDefault(definition.ControlId, 0.0);
            value = Math.Clamp(current + (delta / 127.0), 0.0, 1.0);
            _relativePositions[definition.ControlId] = value;
        }
        else
        {
            value = message.Data2 / 127.0;
        }

        ControlChanged?.Invoke(this, new ControlValueChangedEventArgs(definition.ControlId, definition.Type, value, isOn));
    }

    public void HandleSysEx(byte[] data)
    {
        // Not interpreted for generic devices - no known device-specific meaning.
    }

    public void SetPadColor(string controlId, ControllerColor color)
    {
        if (_output is null || !_byControlId.TryGetValue(controlId, out var definition) || definition.Type != ControlType.Pad)
        {
            return;
        }

        // No generic RGB protocol exists, so this falls back to a plain on/off using the color's
        // brightness as a proxy - the closest a generic driver can get without device-specific
        // knowledge of how it encodes color.
        var isOn = color is not { R: 0, G: 0, B: 0 };
        _output.Send(MidiMessage.NoteOn(definition.Channel, definition.DataNumber, isOn ? (byte)127 : (byte)0));
    }

    public void SetLedRingValue(string controlId, double value, EncoderRingStyle style = EncoderRingStyle.Position)
    {
        if (_output is null || !_byControlId.TryGetValue(controlId, out var definition) || definition.Type != ControlType.Encoder)
        {
            return;
        }

        var midiValue = (int)Math.Clamp(Math.Round(Math.Clamp(value, 0.0, 1.0) * 127), 0, 127);
        _output.Send(MidiMessage.ControlChange(definition.Channel, definition.DataNumber, midiValue));
    }

    public void SetEncoderRingStyle(string controlId, EncoderRingStyle style)
    {
        // No known generic mechanism to select a ring display style independent of its value.
    }

    public void SetButtonLed(string controlId, bool isOn)
    {
        if (_output is null || !_byControlId.TryGetValue(controlId, out var definition) || definition.Type != ControlType.Button)
        {
            return;
        }

        _output.Send(MidiMessage.NoteOn(definition.Channel, definition.DataNumber, isOn ? (byte)127 : (byte)0));
    }

    public void Reset()
    {
        if (_output is null)
        {
            return;
        }

        foreach (var definition in controls)
        {
            switch (definition.Type)
            {
                case ControlType.Pad or ControlType.Button:
                    _output.Send(MidiMessage.NoteOff(definition.Channel, definition.DataNumber));
                    break;
                case ControlType.Encoder or ControlType.Fader:
                    _relativePositions[definition.ControlId] = 0.0;
                    _output.Send(MidiMessage.ControlChange(definition.Channel, definition.DataNumber, 0));
                    break;
            }
        }
    }

    private static Dictionary<RawAddress, GenericControlDefinition> BuildNoteAddressLookup(IReadOnlyList<GenericControlDefinition> controls)
    {
        var lookup = new Dictionary<RawAddress, GenericControlDefinition>();

        foreach (var definition in controls.Where(c => c.Type is ControlType.Pad or ControlType.Button))
        {
            lookup[new RawAddress(definition.Channel, 0x90, definition.DataNumber)] = definition;
            lookup[new RawAddress(definition.Channel, 0x80, definition.DataNumber)] = definition;
        }

        return lookup;
    }

    private static Dictionary<RawAddress, GenericControlDefinition> BuildCcAddressLookup(IReadOnlyList<GenericControlDefinition> controls)
    {
        var lookup = new Dictionary<RawAddress, GenericControlDefinition>();

        foreach (var definition in controls.Where(c => c.Type is ControlType.Encoder or ControlType.Fader))
        {
            lookup[new RawAddress(definition.Channel, 0xB0, definition.DataNumber)] = definition;
        }

        return lookup;
    }

    private sealed record RawAddress(int Channel, int CommandCode, int DataNumber);
}
