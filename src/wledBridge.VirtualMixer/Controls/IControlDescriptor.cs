using wledBridge.VirtualMixer.Models;
using wledBridge.VirtualMixer.State;

namespace wledBridge.VirtualMixer.Controls;

/// <summary>Size constraints in logical pixels; a control is freely resizable between min and max.</summary>
public sealed record ControlSizeRules(
    int MinWidth,
    int MinHeight,
    int MaxWidth,
    int MaxHeight,
    int DefaultWidth,
    int DefaultHeight,
    bool LockSquare = false)
{
    public (int Width, int Height) Clamp(int width, int height)
    {
        var w = Math.Clamp(width, MinWidth, MaxWidth);
        var h = Math.Clamp(height, MinHeight, MaxHeight);

        if (LockSquare)
        {
            var side = Math.Min(w, h);
            return (side, side);
        }

        return (w, h);
    }
}

/// <summary>
/// Registry entry describing one control type. Adding a new control type means implementing
/// this interface (plus a renderer and a settings editor component) and registering it -
/// no changes to the mixer core. Hosts can register custom types via
/// <c>VirtualMixerOptions.AddControlType&lt;T&gt;()</c>.
/// </summary>
public interface IControlDescriptor
{
    /// <summary>Stable unique key, also the JSON discriminator (e.g. "button").</summary>
    string TypeKey { get; }

    string DisplayName { get; }

    /// <summary>Material icon SVG string (MudBlazor Icons.* constant).</summary>
    string Icon { get; }

    ControlSizeRules SizeRules { get; }

    ControlSettingsBase CreateDefaultSettings();

    ControlStateBase CreateInitialState(ControlInstance instance);

    /// <summary>Component rendering the control (used identically by designer and runtime).</summary>
    Type RendererComponent { get; }

    /// <summary>Component for the type-specific section of the designer config sidebar.</summary>
    Type SettingsEditorComponent { get; }

    /// <summary>CLR type of this control's settings (for polymorphic serialization).</summary>
    Type SettingsClrType { get; }
}
