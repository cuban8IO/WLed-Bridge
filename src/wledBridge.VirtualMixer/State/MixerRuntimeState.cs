using System.Collections.Concurrent;
using System.Threading.Channels;
using wledBridge.VirtualMixer.Controls;
using wledBridge.VirtualMixer.Events;
using wledBridge.VirtualMixer.Models;
using wledBridge.VirtualMixer.Services;

namespace wledBridge.VirtualMixer.State;

/// <summary>
/// The runtime state engine (singleton). Holds transient control states for every registered
/// mixer (control IDs are globally unique GUIDs, so states are keyed flat by control ID).
/// The single mutation path is an unbounded command channel consumed by one background task:
/// that consumer applies commands, evaluates bindings (with cycle protection), raises public
/// events and notifies per-control UI subscribers. High-frequency SetValue commands are
/// coalesced per control before processing.
/// </summary>
internal sealed class MixerRuntimeState : IVirtualMixerRuntime, IAsyncDisposable
{
    private const int MaxBindingDepth = 8;

    private readonly ControlRegistry _registry;
    private readonly Channel<IControlCommand> _channel = Channel.CreateUnbounded<IControlCommand>();
    private readonly Task _consumerTask;
    private readonly CancellationTokenSource _cts = new();

    // All dictionaries below are only written on the consumer thread (except registration,
    // which uses concurrent dictionaries to be safe from any caller).
    private readonly ConcurrentDictionary<Guid, ControlStateBase> _states = new();
    private readonly ConcurrentDictionary<Guid, RegisteredControl> _controls = new();
    private readonly ConcurrentDictionary<Guid, RegisteredMixer> _mixers = new();
    private readonly ConcurrentDictionary<Guid, List<WeakSubscription>> _subscriptions = new();

    public MixerRuntimeState(ControlRegistry registry)
    {
        _registry = registry;
        _consumerTask = Task.Run(ConsumeAsync);
    }

    public event EventHandler<ControlEventArgs>? ControlEvent;

    // ------------------------------------------------------------------ registration

    private sealed record RegisteredControl(Guid MixerId, string MixerName, ControlInstance Instance);

    private sealed record RegisteredMixer(MixerDefinition Definition, List<Guid> ControlIds);

    /// <summary>
    /// Registers (or refreshes) a mixer: creates states for new controls, keeps values of
    /// controls whose ID still exists, drops removed ones. Called by the manager on activation
    /// and save, and by runtime components for explicitly displayed mixers.
    /// </summary>
    public void RegisterMixer(MixerDefinition mixer)
    {
        var newIds = new List<Guid>();

        foreach (var control in mixer.AllControls)
        {
            newIds.Add(control.Id);
            _controls[control.Id] = new RegisteredControl(mixer.Id, mixer.Name, control);

            var descriptor = _registry.Find(control.TypeKey);
            if (descriptor is null)
            {
                continue;
            }

            if (!_states.TryGetValue(control.Id, out var existing) ||
                existing.GetType() != descriptor.CreateInitialState(control).GetType())
            {
                _states[control.Id] = descriptor.CreateInitialState(control);
            }
            else if (existing is SubValueState subState && control.Settings is KnobBankSettings bank &&
                     subState.Values.Length != bank.Count)
            {
                var fresh = (SubValueState)descriptor.CreateInitialState(control);
                Array.Copy(subState.Values, fresh.Values, Math.Min(subState.Values.Length, fresh.Values.Length));
                _states[control.Id] = fresh;
            }
        }

        if (_mixers.TryGetValue(mixer.Id, out var previous))
        {
            foreach (var removed in previous.ControlIds.Except(newIds))
            {
                _states.TryRemove(removed, out _);
                _controls.TryRemove(removed, out _);
            }
        }

        _mixers[mixer.Id] = new RegisteredMixer(mixer, newIds);
    }

    public void UnregisterMixer(Guid mixerId)
    {
        if (_mixers.TryRemove(mixerId, out var registered))
        {
            foreach (var controlId in registered.ControlIds)
            {
                _states.TryRemove(controlId, out _);
                _controls.TryRemove(controlId, out _);
            }
        }
    }

    public bool IsMixerRegistered(Guid mixerId) => _mixers.ContainsKey(mixerId);

    // ------------------------------------------------------------------ public command API

    public Task SetControlValueAsync(Guid controlId, int value, CommandSource source = CommandSource.External) =>
        SendCommandAsync(new SetValueCommand(controlId, value, source));

    public Task SetButtonStateAsync(Guid controlId, bool isOn, CommandSource source = CommandSource.External) =>
        SendCommandAsync(new SetButtonStateCommand(controlId, isOn, source));

    public Task SetLedAsync(Guid controlId, LedState led, CommandSource source = CommandSource.External) =>
        SendCommandAsync(new SetLedCommand(controlId, led, source));

    public Task SetSubValueAsync(Guid controlId, int subIndex, int value, CommandSource source = CommandSource.External) =>
        SendCommandAsync(new SetSubValueCommand(controlId, subIndex, value, source));

    public Task SendCommandAsync(IControlCommand command)
    {
        _channel.Writer.TryWrite(command);
        return Task.CompletedTask;
    }

    public ControlStateSnapshot? GetControlState(Guid controlId)
    {
        if (!_states.TryGetValue(controlId, out var state) || !_controls.TryGetValue(controlId, out var control))
        {
            return null;
        }

        return state switch
        {
            ButtonState b => new ControlStateSnapshot(controlId, control.Instance.TypeKey, null, b.IsOn, b.LedBrightness, b.LedColorHex, null),
            ValueState v => new ControlStateSnapshot(controlId, control.Instance.TypeKey, v.Value, null, null, v.ColorHex, null, v.RingValue),
            SubValueState s => new ControlStateSnapshot(controlId, control.Instance.TypeKey, null, null, null, null, [.. s.Values]),
            _ => null
        };
    }

    // ------------------------------------------------------------------ UI subscriptions

    private sealed class WeakSubscription(Action callback) : IDisposable
    {
        public Action? Callback { get; private set; } = callback;
        public void Dispose() => Callback = null;
    }

    /// <summary>Per-control change notification for renderer components (render isolation).</summary>
    public IDisposable SubscribeControl(Guid controlId, Action onChanged)
    {
        var subscription = new WeakSubscription(onChanged);
        var list = _subscriptions.GetOrAdd(controlId, _ => []);
        lock (list)
        {
            list.Add(subscription);
            list.RemoveAll(s => s.Callback is null);
        }

        return subscription;
    }

    private void NotifyControl(Guid controlId)
    {
        if (!_subscriptions.TryGetValue(controlId, out var list))
        {
            return;
        }

        WeakSubscription[] snapshot;
        lock (list)
        {
            snapshot = [.. list];
        }

        foreach (var subscription in snapshot)
        {
            try
            {
                subscription.Callback?.Invoke();
            }
            catch
            {
                // A dead circuit must never break the pipeline.
            }
        }
    }

    // ------------------------------------------------------------------ consumer

    private async Task ConsumeAsync()
    {
        var batch = new List<IControlCommand>();

        try
        {
            while (await _channel.Reader.WaitToReadAsync(_cts.Token))
            {
                batch.Clear();
                while (_channel.Reader.TryRead(out var command))
                {
                    batch.Add(command);
                }

                foreach (var command in Coalesce(batch))
                {
                    try
                    {
                        Apply(command, depth: 0);
                    }
                    catch
                    {
                        // One bad command must never kill the engine.
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown.
        }
    }

    /// <summary>Keeps only the last SetValue/SetSubValue per target within one drained batch.</summary>
    private static IEnumerable<IControlCommand> Coalesce(List<IControlCommand> batch)
    {
        if (batch.Count < 2)
        {
            return batch;
        }

        var lastValueIndex = new Dictionary<(Guid, int), int>();
        for (var i = 0; i < batch.Count; i++)
        {
            switch (batch[i])
            {
                case SetValueCommand c:
                    lastValueIndex[(c.ControlId, -1)] = i;
                    break;
                case SetSubValueCommand c:
                    lastValueIndex[(c.ControlId, c.SubIndex)] = i;
                    break;
            }
        }

        var result = new List<IControlCommand>(batch.Count);
        for (var i = 0; i < batch.Count; i++)
        {
            var keep = batch[i] switch
            {
                SetValueCommand c => lastValueIndex[(c.ControlId, -1)] == i,
                SetSubValueCommand c => lastValueIndex[(c.ControlId, c.SubIndex)] == i,
                _ => true
            };
            if (keep)
            {
                result.Add(batch[i]);
            }
        }

        return result;
    }

    private void Apply(IControlCommand command, int depth)
    {
        if (!_controls.TryGetValue(command.ControlId, out var control) ||
            !_states.TryGetValue(command.ControlId, out var state))
        {
            return;
        }

        switch (command)
        {
            case SetValueCommand c:
                ApplySetValue(control, state, c.Value, c.Source, depth);
                break;

            case SetSubValueCommand c:
                ApplySetSubValue(control, state, c.SubIndex, c.Value, c.Source, depth);
                break;

            case SetButtonStateCommand c when state is ButtonState button:
                if (button.IsOn != c.IsOn)
                {
                    button.IsOn = c.IsOn;
                    Raise(new ControlStateChangedEventArgs
                    {
                        MixerId = control.MixerId, MixerName = control.MixerName,
                        ControlId = control.Instance.Id, ControlName = control.Instance.Name,
                        TypeKey = control.Instance.TypeKey, Source = c.Source,
                        OldState = c.IsOn ? "Off" : "On", NewState = c.IsOn ? "On" : "Off"
                    });
                    NotifyControl(control.Instance.Id);
                }
                break;

            case SetLedCommand c:
                ApplySetLed(control, state, c.Led, c.Source);
                break;

            case ButtonPressCommand c when state is ButtonState button:
                ApplyButtonPress(control, button, c.Source);
                break;

            case ButtonReleaseCommand c when state is ButtonState button:
                if (control.Instance.Settings is ButtonSettings { Mode: ButtonMode.Momentary } && button.IsOn)
                {
                    button.IsOn = false;
                    Raise(new ControlReleasedEventArgs
                    {
                        MixerId = control.MixerId, MixerName = control.MixerName,
                        ControlId = control.Instance.Id, ControlName = control.Instance.Name,
                        TypeKey = control.Instance.TypeKey, Source = c.Source
                    });
                    NotifyControl(control.Instance.Id);
                }
                break;

            case KnobDeltaCommand c:
                Raise(new ControlDeltaChangedEventArgs
                {
                    MixerId = control.MixerId, MixerName = control.MixerName,
                    ControlId = control.Instance.Id, ControlName = control.Instance.Name,
                    TypeKey = control.Instance.TypeKey, SubIndex = c.SubIndex, Source = c.Source,
                    Delta = c.Delta
                });
                break;
        }
    }

    private void ApplyButtonPress(RegisteredControl control, ButtonState button, CommandSource source)
    {
        var mode = (control.Instance.Settings as ButtonSettings)?.Mode ?? ButtonMode.Momentary;

        switch (mode)
        {
            case ButtonMode.Momentary:
                if (!button.IsOn)
                {
                    button.IsOn = true;
                    Raise(new ControlPressedEventArgs
                    {
                        MixerId = control.MixerId, MixerName = control.MixerName,
                        ControlId = control.Instance.Id, ControlName = control.Instance.Name,
                        TypeKey = control.Instance.TypeKey, Source = source
                    });
                    NotifyControl(control.Instance.Id);
                }
                break;

            case ButtonMode.Toggle:
                button.IsOn = !button.IsOn;
                Raise(new ControlStateChangedEventArgs
                {
                    MixerId = control.MixerId, MixerName = control.MixerName,
                    ControlId = control.Instance.Id, ControlName = control.Instance.Name,
                    TypeKey = control.Instance.TypeKey, Source = source,
                    OldState = button.IsOn ? "Off" : "On", NewState = button.IsOn ? "On" : "Off"
                });
                NotifyControl(control.Instance.Id);
                break;

            case ButtonMode.Trigger:
                Raise(new ControlPressedEventArgs
                {
                    MixerId = control.MixerId, MixerName = control.MixerName,
                    ControlId = control.Instance.Id, ControlName = control.Instance.Name,
                    TypeKey = control.Instance.TypeKey, Source = source
                });
                break;
        }
    }

    private void ApplySetValue(RegisteredControl control, ControlStateBase state, int value, CommandSource source, int depth)
    {
        if (control.Instance.Settings is KnobSettings { Mode: KnobMode.Relative })
        {
            Raise(new ControlCommandRejectedEventArgs
            {
                MixerId = control.MixerId, MixerName = control.MixerName,
                ControlId = control.Instance.Id, ControlName = control.Instance.Name,
                TypeKey = control.Instance.TypeKey, Source = source,
                Reason = "SetValue ignoriert: Regler ist im Relativ-Modus"
            });
            return;
        }

        if (state is not ValueState valueState)
        {
            return;
        }

        value = Math.Clamp(value, 0, 127);
        if (valueState.Value == value)
        {
            return;
        }

        var old = valueState.Value;
        valueState.Value = value;

        Raise(new ControlValueChangedEventArgs
        {
            MixerId = control.MixerId, MixerName = control.MixerName,
            ControlId = control.Instance.Id, ControlName = control.Instance.Name,
            TypeKey = control.Instance.TypeKey, Source = source,
            OldValue = old, NewValue = value
        });
        NotifyControl(control.Instance.Id);
        EvaluateBindings(control, subIndex: null, value, depth);
    }

    private void ApplySetSubValue(RegisteredControl control, ControlStateBase state, int subIndex, int value, CommandSource source, int depth)
    {
        // A single-value control (knob) exposes its LED ring as sub-index 0, so bindings and the
        // runtime API can drive the ring independently of the knob position.
        if (state is ValueState ringState && subIndex == 0)
        {
            value = Math.Clamp(value, 0, 127);
            if (ringState.RingValue == value)
            {
                return;
            }

            var oldRing = ringState.RingValue;
            ringState.RingValue = value;

            Raise(new ControlValueChangedEventArgs
            {
                MixerId = control.MixerId, MixerName = control.MixerName,
                ControlId = control.Instance.Id, ControlName = control.Instance.Name,
                TypeKey = control.Instance.TypeKey, SubIndex = subIndex, Source = source,
                OldValue = oldRing, NewValue = value
            });
            NotifyControl(control.Instance.Id);
            EvaluateBindings(control, subIndex, value, depth);
            return;
        }

        if (state is not SubValueState subState || subIndex < 0 || subIndex >= subState.Values.Length)
        {
            return;
        }

        value = Math.Clamp(value, 0, 127);
        if (subState.Values[subIndex] == value)
        {
            return;
        }

        var old = subState.Values[subIndex];
        subState.Values[subIndex] = value;

        Raise(new ControlValueChangedEventArgs
        {
            MixerId = control.MixerId, MixerName = control.MixerName,
            ControlId = control.Instance.Id, ControlName = control.Instance.Name,
            TypeKey = control.Instance.TypeKey, SubIndex = subIndex, Source = source,
            OldValue = old, NewValue = value
        });
        NotifyControl(control.Instance.Id);
        EvaluateBindings(control, subIndex, value, depth);
    }

    private void ApplySetLed(RegisteredControl control, ControlStateBase state, LedState led, CommandSource source)
    {
        var brightness = Math.Clamp(led.Brightness, 0, 127);
        string oldText;
        string newText;

        switch (state)
        {
            case ButtonState button:
                oldText = FormatLed(button.LedBrightness, button.LedColorHex);
                button.LedBrightness = brightness;
                if (led.ColorHex is not null)
                {
                    button.LedColorHex = led.ColorHex;
                }
                newText = FormatLed(button.LedBrightness, button.LedColorHex);
                break;

            case ValueState valueState:
                oldText = FormatLed(valueState.Value, valueState.ColorHex);
                if (led.ColorHex is not null)
                {
                    valueState.ColorHex = led.ColorHex;
                }
                newText = FormatLed(valueState.Value, valueState.ColorHex);
                break;

            default:
                return;
        }

        if (oldText == newText)
        {
            return;
        }

        Raise(new ControlStateChangedEventArgs
        {
            MixerId = control.MixerId, MixerName = control.MixerName,
            ControlId = control.Instance.Id, ControlName = control.Instance.Name,
            TypeKey = control.Instance.TypeKey, Source = source,
            OldState = oldText, NewState = newText
        });
        NotifyControl(control.Instance.Id);
    }

    private static string FormatLed(int brightness, string? colorHex) =>
        colorHex is null ? $"LED {brightness}" : $"LED {brightness} {colorHex}";

    private void EvaluateBindings(RegisteredControl source, int? subIndex, int value, int depth)
    {
        if (depth >= MaxBindingDepth || !_mixers.TryGetValue(source.MixerId, out var mixer))
        {
            return;
        }

        foreach (var binding in mixer.Definition.Bindings)
        {
            if (binding.SourceControlId != source.Instance.Id || binding.SourceSubIndex != subIndex)
            {
                continue;
            }

            var transformed = binding.Transform.Apply(value);
            IControlCommand command = binding.TargetSubIndex is { } targetSub
                ? new SetSubValueCommand(binding.TargetControlId, targetSub, transformed, CommandSource.Binding)
                : new SetValueCommand(binding.TargetControlId, transformed, CommandSource.Binding);

            // Applied synchronously (same consumer thread) with increased depth for cycle protection.
            Apply(command, depth + 1);
        }
    }

    private void Raise(ControlEventArgs args)
    {
        try
        {
            ControlEvent?.Invoke(this, args);
        }
        catch
        {
            // Subscriber exceptions must never break the pipeline.
        }
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        _cts.Cancel();
        try
        {
            await _consumerTask;
        }
        catch
        {
            // Shutdown.
        }
        _cts.Dispose();
    }
}
