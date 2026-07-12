using Microsoft.Extensions.Options;
using wledBridge.VirtualMixer.Events;
using wledBridge.VirtualMixer.Services;

namespace wledBridge.VirtualMixer.State;

/// <summary>
/// Bounded, transient event log for the designer's event monitor. A classic ring buffer:
/// O(1) append, fixed capacity, oldest entries overwritten. Decoupled from controls - it is
/// just one more subscriber of the public runtime event stream.
/// </summary>
internal sealed class EventMonitorBuffer : IDisposable
{
    private readonly IVirtualMixerRuntime _runtime;
    private readonly ControlEventArgs?[] _buffer;
    private readonly Lock _lock = new();
    private int _next;
    private int _count;

    public EventMonitorBuffer(IVirtualMixerRuntime runtime, IOptions<VirtualMixerOptions> options)
    {
        _runtime = runtime;
        _buffer = new ControlEventArgs?[Math.Max(10, options.Value.EventMonitorCapacity)];
        _runtime.ControlEvent += OnControlEvent;
    }

    public event Action? Changed;

    private void OnControlEvent(object? sender, ControlEventArgs args)
    {
        lock (_lock)
        {
            _buffer[_next] = args;
            _next = (_next + 1) % _buffer.Length;
            _count = Math.Min(_count + 1, _buffer.Length);
        }

        Changed?.Invoke();
    }

    /// <summary>Entries newest-first.</summary>
    public IReadOnlyList<ControlEventArgs> GetEntries()
    {
        lock (_lock)
        {
            var result = new List<ControlEventArgs>(_count);
            for (var i = 0; i < _count; i++)
            {
                var index = (_next - 1 - i + (_buffer.Length * 2)) % _buffer.Length;
                if (_buffer[index] is { } entry)
                {
                    result.Add(entry);
                }
            }

            return result;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            Array.Clear(_buffer);
            _next = 0;
            _count = 0;
        }

        Changed?.Invoke();
    }

    public void Dispose() => _runtime.ControlEvent -= OnControlEvent;
}
