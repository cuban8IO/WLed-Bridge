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

/// <summary>
/// How a lit element (statusbar / button LED) renders its value. Shared by statusbar and the
/// button status LED so both offer the same visual behaviour.
/// </summary>
public enum LedDisplayMode
{
    /// <summary>Fill proportional to the value with a hard edge (classic bar).</summary>
    Bar,

    /// <summary>Fill proportional to the value, fading out towards the leading edge.</summary>
    Fade,

    /// <summary>Whole element lit; brightness scales with the value.</summary>
    Solid,

    /// <summary>Whole element pulsing; the value scales the pulse intensity.</summary>
    Pulse
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

    /// <summary>Same set of display behaviours as the statusbar (Solid/Pulse most useful here).</summary>
    public LedDisplayMode DisplayMode { get; set; } = LedDisplayMode.Solid;
}

public class ButtonSettings : ControlSettingsBase
{
    public ButtonMode Mode { get; set; } = ButtonMode.Momentary;
    public LedConfig? Led { get; set; }

    /// <summary>When true, the LED lights automatically while the button is active (no external command needed).</summary>
    public bool LedFollowsState { get; set; }

    /// <summary>When true, the LED colour glows across the whole button instead of a corner dot.</summary>
    public bool LedFillButton { get; set; }
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

    /// <summary>Bar (proportional), Fade (gradient), Solid (whole lit) or Pulse (pulsing).</summary>
    public LedDisplayMode DisplayMode { get; set; } = LedDisplayMode.Bar;
}

// ---------------------------------------------------------------------------- Knob

public enum KnobMode
{
    /// <summary>Value state 0..127, curve applies, ring shows position.</summary>
    Absolute,

    /// <summary>Stateless; interactions emit delta events only.</summary>
    Relative
}

/// <summary>What drives a knob's optional LED ring.</summary>
public enum KnobRingSource
{
    /// <summary>The ring follows the knob's own value.</summary>
    KnobValue,

    /// <summary>The ring shows an independent value, set externally or via a binding (sub-index 0).</summary>
    External
}

public class KnobSettings : ControlSettingsBase
{
    public KnobMode Mode { get; set; } = KnobMode.Absolute;
    public Curve Curve { get; set; } = new();

    /// <summary>Optional LED ring drawn around the knob.</summary>
    public bool LedRingEnabled { get; set; }
    public LedColorMode LedRingColorMode { get; set; } = LedColorMode.Amber;
    public string? LedRingColorHex { get; set; }
    public KnobRingSource LedRingSource { get; set; } = KnobRingSource.KnobValue;
}

// ---------------------------------------------------------------------------- Line

public class LineSettings : ControlSettingsBase
{
    public ControlOrientation Orientation { get; set; } = ControlOrientation.Horizontal;

    /// <summary>Line thickness in logical pixels.</summary>
    public int Thickness { get; set; } = 3;

    /// <summary>Line colour; null falls back to the theme's default line colour.</summary>
    public string? ColorHex { get; set; }
}

// ---------------------------------------------------------------------------- Label

public class LabelSettings : ControlSettingsBase
{
    /// <summary>Text colour; null falls back to the theme's primary text colour.</summary>
    public string? ColorHex { get; set; }

    public int FontSize { get; set; } = 14;

    /// <summary>CSS font-family; null falls back to the theme font.</summary>
    public string? FontFamily { get; set; }

    public bool Bold { get; set; }

    /// <summary>CSS text-align: left / center / right.</summary>
    public string TextAlign { get; set; } = "center";
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
