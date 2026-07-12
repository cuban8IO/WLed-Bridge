namespace wledBridge.VirtualMixer.Models;

public enum BindingTransformType
{
    Identity,
    Invert,
    RangeMap,
    Curve,
    Threshold
}

/// <summary>
/// Declarative value transformation of a binding: source value in (0..127) -> target value out (0..127).
/// </summary>
public class BindingTransform
{
    public BindingTransformType Type { get; set; } = BindingTransformType.Identity;

    // RangeMap
    public int InMin { get; set; }
    public int InMax { get; set; } = 127;
    public int OutMin { get; set; }
    public int OutMax { get; set; } = 127;

    // Curve
    public Curve Curve { get; set; } = new() { Type = CurveType.Exponential };

    // Threshold: value >= Threshold -> 127, else 0
    public int Threshold { get; set; } = 64;

    public int Apply(int value)
    {
        value = Math.Clamp(value, 0, 127);
        return Type switch
        {
            BindingTransformType.Invert => 127 - value,
            BindingTransformType.RangeMap => ApplyRangeMap(value),
            BindingTransformType.Curve => Curve.Apply(value / 127.0),
            BindingTransformType.Threshold => value >= Threshold ? 127 : 0,
            _ => value
        };
    }

    private int ApplyRangeMap(int value)
    {
        if (InMax == InMin)
        {
            return Math.Clamp(OutMin, 0, 127);
        }

        var t = Math.Clamp((value - InMin) / (double)(InMax - InMin), 0.0, 1.0);
        return (int)Math.Clamp(Math.Round(OutMin + (t * (OutMax - OutMin))), 0, 127);
    }
}

/// <summary>
/// Generic control binding: whenever the source control's value changes, the transformed value
/// is applied to the target control. Part of the mixer definition (persisted).
/// </summary>
public class ControlBinding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SourceControlId { get; set; }
    public int? SourceSubIndex { get; set; }
    public Guid TargetControlId { get; set; }
    public int? TargetSubIndex { get; set; }
    public BindingTransform Transform { get; set; } = new();
}
