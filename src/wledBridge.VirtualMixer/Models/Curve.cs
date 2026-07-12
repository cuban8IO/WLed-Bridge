namespace wledBridge.VirtualMixer.Models;

public enum CurveType
{
    Linear,
    Exponential,
    Logarithmic
}

/// <summary>
/// Response curve mapping a normalized position p in [0,1] to a MIDI-style value 0..127.
/// Linear: p - Exponential: p^gamma - Logarithmic: p^(1/gamma). One well-defined parameter,
/// no arbitrary formulas.
/// </summary>
public class Curve
{
    public CurveType Type { get; set; } = CurveType.Linear;

    /// <summary>Curve steepness, >= 1. Ignored for <see cref="CurveType.Linear"/>.</summary>
    public double Gamma { get; set; } = 2.0;

    public int Apply(double normalizedPosition)
    {
        var p = Math.Clamp(normalizedPosition, 0.0, 1.0);
        var f = Type switch
        {
            CurveType.Exponential => Math.Pow(p, EffectiveGamma),
            CurveType.Logarithmic => Math.Pow(p, 1.0 / EffectiveGamma),
            _ => p
        };
        return (int)Math.Clamp(Math.Round(f * 127.0), 0, 127);
    }

    public double ToPosition(int value)
    {
        var f = Math.Clamp(value, 0, 127) / 127.0;
        return Type switch
        {
            CurveType.Exponential => Math.Pow(f, 1.0 / EffectiveGamma),
            CurveType.Logarithmic => Math.Pow(f, EffectiveGamma),
            _ => f
        };
    }

    private double EffectiveGamma => Math.Max(1.0, Gamma);

    public Curve Clone() => new() { Type = Type, Gamma = Gamma };
}
