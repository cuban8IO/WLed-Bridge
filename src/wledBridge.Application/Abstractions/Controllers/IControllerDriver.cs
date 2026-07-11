using wledBridge.Application.Abstractions.Midi;

namespace wledBridge.Application.Abstractions.Controllers;

public interface IControllerDriver
{
    string DriverKey { get; }

    string DisplayName { get; }

    IReadOnlyList<ControlDescriptor> ControlLayout { get; }

    /// <summary>
    /// Exports this driver's full control set as raw learned-style definitions (address,
    /// classification, LED capability) - used to seed a new editable "Generic" device from a
    /// fixed driver's built-in layout (see the "Aus Vorlage erstellen" workflow).
    /// </summary>
    IReadOnlyList<GenericControlDefinition> ExportControlDefinitions();

    void Attach(IMidiOutputPort output);

    void Detach();

    void HandleMessage(MidiMessage message);

    void HandleSysEx(byte[] data);

    void SetPadColor(string controlId, ControllerColor color);

    void SetLedRingValue(string controlId, double value, EncoderRingStyle style = EncoderRingStyle.Position);

    void SetEncoderRingStyle(string controlId, EncoderRingStyle style);

    void SetButtonLed(string controlId, bool isOn);

    void Reset();

    event EventHandler<ControlValueChangedEventArgs>? ControlChanged;
}
