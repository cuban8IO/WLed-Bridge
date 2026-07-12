using wledBridge.VirtualMixer.Models;

namespace wledBridge.VirtualMixer.Events;

public sealed class MixerActivatedEventArgs : EventArgs
{
    public MixerInfo? ActiveMixer { get; init; }
}
