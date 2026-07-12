using wledBridge.VirtualMixer.State;

namespace wledBridge.VirtualMixer.Services;

public enum SimulationPattern
{
    Sine,
    Random,
    Sweep
}

/// <summary>
/// Demo/test value generator. Deliberately drives controls exclusively through the public
/// <see cref="IVirtualMixerRuntime"/> API, so running it also exercises the external-control
/// path end to end.
/// </summary>
internal sealed class SimulationService(IVirtualMixerRuntime runtime) : IDisposable
{
    private readonly Lock _lock = new();
    private readonly Dictionary<(Guid ControlId, int? SubIndex), double> _phases = [];
    private readonly Random _random = new();
    private Timer? _timer;
    private List<(Guid ControlId, int? SubIndex)> _targets = [];
    private SimulationPattern _pattern = SimulationPattern.Sine;
    private int _sweepDirection = 1;
    private int _sweepValue;

    public bool IsRunning { get; private set; }

    public event EventHandler? StateChanged;

    public void Start(IEnumerable<(Guid ControlId, int? SubIndex)> targets, SimulationPattern pattern)
    {
        lock (_lock)
        {
            _targets = [.. targets];
            _pattern = pattern;
            _phases.Clear();
            var offset = 0.0;
            foreach (var target in _targets)
            {
                _phases[target] = offset;
                offset += 0.7;
            }

            _timer?.Dispose();
            _timer = new Timer(Tick, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(50));
            IsRunning = true;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        lock (_lock)
        {
            _timer?.Dispose();
            _timer = null;
            IsRunning = false;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Tick(object? state)
    {
        List<(Guid ControlId, int? SubIndex)> targets;
        SimulationPattern pattern;
        lock (_lock)
        {
            if (!IsRunning)
            {
                return;
            }

            targets = _targets;
            pattern = _pattern;

            if (pattern == SimulationPattern.Sweep)
            {
                _sweepValue += _sweepDirection * 3;
                if (_sweepValue >= 127) { _sweepValue = 127; _sweepDirection = -1; }
                if (_sweepValue <= 0) { _sweepValue = 0; _sweepDirection = 1; }
            }
        }

        foreach (var target in targets)
        {
            var value = pattern switch
            {
                SimulationPattern.Random => _random.Next(0, 128),
                SimulationPattern.Sweep => _sweepValue,
                _ => SineValue(target)
            };

            if (target.SubIndex is { } subIndex)
            {
                _ = runtime.SetSubValueAsync(target.ControlId, subIndex, value, CommandSource.Simulation);
            }
            else
            {
                _ = runtime.SetControlValueAsync(target.ControlId, value, CommandSource.Simulation);
            }
        }
    }

    private int SineValue((Guid ControlId, int? SubIndex) target)
    {
        lock (_lock)
        {
            var phase = _phases.GetValueOrDefault(target);
            phase += 0.08;
            _phases[target] = phase;
            return (int)Math.Round((Math.Sin(phase) + 1.0) / 2.0 * 127.0);
        }
    }

    public void Dispose() => _timer?.Dispose();
}
