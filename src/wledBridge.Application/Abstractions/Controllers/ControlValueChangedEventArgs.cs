using wledBridge.Domain.Controllers;

namespace wledBridge.Application.Abstractions.Controllers;

public class ControlValueChangedEventArgs(string controlId, ControlType type, double value, bool? isOn = null) : EventArgs
{
    public string ControlId { get; } = controlId;

    public ControlType Type { get; } = type;

    public double Value { get; } = value;

    public bool? IsOn { get; } = isOn;
}
