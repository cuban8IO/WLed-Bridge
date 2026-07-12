namespace wledBridge.VirtualMixer.Controls;

/// <summary>Type keys and descriptor list of the built-in control types.</summary>
public static class BuiltInControlTypes
{
    public const string Button = "button";
    public const string Fader = "fader";
    public const string Statusbar = "statusbar";
    public const string Knob = "knob";
    public const string KnobBank = "knobbank";

    /// <summary>Descriptor types registered by <c>AddVirtualMixer()</c>.</summary>
    internal static readonly IReadOnlyList<Type> Descriptors =
    [
        typeof(ButtonDescriptor),
        typeof(FaderDescriptor),
        typeof(StatusbarDescriptor),
        typeof(KnobDescriptor),
        typeof(KnobBankDescriptor)
    ];
}
