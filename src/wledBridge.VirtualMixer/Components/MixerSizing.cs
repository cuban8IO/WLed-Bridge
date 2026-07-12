namespace wledBridge.VirtualMixer.Components;

/// <summary>
/// Sizing modes of the runtime component. Scaling never mutates stored positions or sizes -
/// it is purely a render-time CSS transform.
/// </summary>
public enum MixerSizing
{
    /// <summary>Natural logical size of the mixer.</summary>
    Natural,

    /// <summary>Fixed width; scrollbars if the mixer is larger.</summary>
    FixedWidth,

    /// <summary>Fixed height; scrollbars if the mixer is larger.</summary>
    FixedHeight,

    /// <summary>Fixed width and height; scrollbars if the mixer is larger.</summary>
    Fixed,

    /// <summary>Scales the mixer down/up to fit its parent container (no scrollbars).</summary>
    FitToContainer
}
