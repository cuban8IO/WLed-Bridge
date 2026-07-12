namespace wledBridge.VirtualMixer.Models;

/// <summary>
/// Base for type-specific control settings. Serialized polymorphically: the JSON discriminator
/// is the owning <see cref="ControlInstance.TypeKey"/>, resolved through the control registry,
/// so host-registered custom control types serialize without module changes.
/// </summary>
public abstract class ControlSettingsBase
{
}

public enum ControlOrientation
{
    Vertical,
    Horizontal
}

public enum LedColorMode
{
    Amber,
    Rgb
}

// ---------------------------------------------------------------------------- Button

public enum ButtonMode
{
    /// <summary>Active while pressed; raises Pressed/Released.</summary>
    Momentary,

    /// <summary>State flips per press; raises StateChanged.</summary>
    Toggle,

    /// <summary>Stateless; raises only a Pressed event per activation.</summary>
    Trigger
}

public class LedConfig
{
    public LedColorMode ColorMode { get; set; } = LedColorMode.Amber;

    /// <summary>Configured color for <see cref="LedColorMode.Rgb"/> (hex, e.g. "#FF0000").</summary>
    public string? ColorHex { get; set; }
}

public class ButtonSettings : ControlSettingsBase
{
    public ButtonMode Mode { get; set; } = ButtonMode.Momentary;
    public LedConfig? Led { get; set; }
}

// ---------------------------------------------------------------------------- Fader

public class FaderSettings : ControlSettingsBase
{
    public ControlOrientation Orientation { get; set; } = ControlOrientation.Vertical;
    public Curve Curve { get; set; } = new();
}

// ---------------------------------------------------------------------------- Statusbar

public class StatusbarSettings : ControlSettingsBase
{
    public LedColorMode ColorMode { get; set; } = LedColorMode.Amber;
    public string? ColorHex { get; set; }
    public ControlOrientation Orientation { get; set; } = ControlOrientation.Horizontal;
}

// ---------------------------------------------------------------------------- Knob

public enum KnobMode
{
    /// <summary>Value state 0..127, curve applies, ring shows position.</summary>
    Absolute,

    /// <summary>Stateless; interactions emit delta events only.</summary>
    Relative
}

public class KnobSettings : ControlSettingsBase
{
    public KnobMode Mode { get; set; } = KnobMode.Absolute;
    public Curve Curve { get; set; } = new();
}

// ---------------------------------------------------------------------------- Knob bank

public enum KnobBankLayout
{
    Auto,
    FixedColumns,
    FixedRows,
    Horizontal,
    Vertical
}

/// <summary>Per-mini-knob overrides inside a knob bank; null means "inherit from the bank".</summary>
public class KnobItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? Label { get; set; }
    public string? ColorHex { get; set; }
    public KnobMode? ModeOverride { get; set; }
    public Curve? CurveOverride { get; set; }
}

public class KnobBankSettings : ControlSettingsBase
{
    public int Count { get; set; } = 4;
    public KnobBankLayout Layout { get; set; } = KnobBankLayout.Auto;
    public int FixedColumns { get; set; } = 2;
    public int FixedRows { get; set; } = 2;
    public KnobMode Mode { get; set; } = KnobMode.Absolute;
    public Curve Curve { get; set; } = new();
    public List<KnobItem> Knobs { get; set; } = [];

    /// <summary>Keeps <see cref="Knobs"/> index-aligned with <see cref="Count"/>.</summary>
    public void EnsureKnobItems()
    {
        while (Knobs.Count < Count)
        {
            Knobs.Add(new KnobItem());
        }

        if (Knobs.Count > Count)
        {
            Knobs.RemoveRange(Count, Knobs.Count - Count);
        }
    }
}
