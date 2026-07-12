using Microsoft.Extensions.Options;
using wledBridge.VirtualMixer.Controls;
using wledBridge.VirtualMixer.Events;
using wledBridge.VirtualMixer.Models;
using wledBridge.VirtualMixer.State;
using Xunit;

namespace wledBridge.VirtualMixer.Tests;

public class RuntimeEngineTests : IAsyncDisposable
{
    private readonly MixerRuntimeState _runtime = new(TestSetup.CreateRegistry());

    public async ValueTask DisposeAsync() => await _runtime.DisposeAsync();

    [Fact]
    public async Task SetValue_from_foreign_threads_is_consistent()
    {
        var fader = TestSetup.CreateControl(BuiltInControlTypes.Fader, new FaderSettings());
        _runtime.RegisterMixer(TestSetup.CreateMixer(fader));

        var tasks = Enumerable.Range(0, 20).Select(i => Task.Run(async () =>
        {
            for (var j = 0; j <= 127; j++)
            {
                await _runtime.SetControlValueAsync(fader.Id, j);
            }
        }));
        await Task.WhenAll(tasks);

        await TestSetup.WaitForAsync(() => _runtime.GetControlState(fader.Id)?.Value == 127);
        Assert.Equal(127, _runtime.GetControlState(fader.Id)!.Value);
    }

    [Fact]
    public async Task Value_change_raises_typed_event_with_old_and_new()
    {
        var fader = TestSetup.CreateControl(BuiltInControlTypes.Fader, new FaderSettings());
        _runtime.RegisterMixer(TestSetup.CreateMixer(fader));

        ControlValueChangedEventArgs? received = null;
        _runtime.ControlEvent += (_, e) =>
        {
            if (e is ControlValueChangedEventArgs v)
            {
                received = v;
            }
        };

        await _runtime.SetControlValueAsync(fader.Id, 100);
        await TestSetup.WaitForAsync(() => received is not null);

        Assert.Equal(0, received!.OldValue);
        Assert.Equal(100, received.NewValue);
        Assert.Equal("Testmixer", received.MixerName);
    }

    [Fact]
    public async Task SetValue_on_relative_knob_is_rejected_with_event()
    {
        var knob = TestSetup.CreateControl(BuiltInControlTypes.Knob, new KnobSettings { Mode = KnobMode.Relative });
        _runtime.RegisterMixer(TestSetup.CreateMixer(knob));

        ControlCommandRejectedEventArgs? rejected = null;
        _runtime.ControlEvent += (_, e) =>
        {
            if (e is ControlCommandRejectedEventArgs r)
            {
                rejected = r;
            }
        };

        await _runtime.SetControlValueAsync(knob.Id, 64);
        await TestSetup.WaitForAsync(() => rejected is not null);

        Assert.Contains("Relativ", rejected!.Reason);
    }

    [Fact]
    public async Task Binding_transfers_transformed_value_to_target()
    {
        var fader = TestSetup.CreateControl(BuiltInControlTypes.Fader, new FaderSettings(), "Quelle");
        var statusbar = TestSetup.CreateControl(BuiltInControlTypes.Statusbar, new StatusbarSettings(), "Ziel");
        var mixer = TestSetup.CreateMixer(fader, statusbar);
        mixer.Bindings.Add(new ControlBinding
        {
            SourceControlId = fader.Id,
            TargetControlId = statusbar.Id,
            Transform = new BindingTransform { Type = BindingTransformType.Invert }
        });
        _runtime.RegisterMixer(mixer);

        await _runtime.SetControlValueAsync(fader.Id, 27);
        await TestSetup.WaitForAsync(() => _runtime.GetControlState(statusbar.Id)?.Value == 100);

        Assert.Equal(100, _runtime.GetControlState(statusbar.Id)!.Value);
    }

    [Fact]
    public async Task Binding_cycles_are_stopped_by_depth_limit()
    {
        var a = TestSetup.CreateControl(BuiltInControlTypes.Fader, new FaderSettings(), "A");
        var b = TestSetup.CreateControl(BuiltInControlTypes.Fader, new FaderSettings(), "B");
        var mixer = TestSetup.CreateMixer(a, b);
        mixer.Bindings.Add(new ControlBinding
        {
            SourceControlId = a.Id, TargetControlId = b.Id,
            Transform = new BindingTransform { Type = BindingTransformType.Invert }
        });
        mixer.Bindings.Add(new ControlBinding
        {
            SourceControlId = b.Id, TargetControlId = a.Id,
            Transform = new BindingTransform { Type = BindingTransformType.Invert }
        });
        _runtime.RegisterMixer(mixer);

        // Must terminate (depth limit) instead of looping forever.
        await _runtime.SetControlValueAsync(a.Id, 30);
        await TestSetup.WaitForAsync(() => _runtime.GetControlState(b.Id)?.Value is not 0);

        Assert.NotNull(_runtime.GetControlState(a.Id));
        Assert.NotNull(_runtime.GetControlState(b.Id));
    }

    [Fact]
    public async Task Toggle_button_press_flips_state()
    {
        var button = TestSetup.CreateControl(BuiltInControlTypes.Button, new ButtonSettings { Mode = ButtonMode.Toggle });
        _runtime.RegisterMixer(TestSetup.CreateMixer(button));

        await _runtime.SendCommandAsync(new ButtonPressCommand(button.Id));
        await TestSetup.WaitForAsync(() => _runtime.GetControlState(button.Id)?.IsOn == true);

        await _runtime.SendCommandAsync(new ButtonPressCommand(button.Id));
        await TestSetup.WaitForAsync(() => _runtime.GetControlState(button.Id)?.IsOn == false);
    }

    [Fact]
    public async Task Knob_bank_sub_values_are_independent()
    {
        var bank = TestSetup.CreateControl(BuiltInControlTypes.KnobBank, new KnobBankSettings { Count = 4 });
        _runtime.RegisterMixer(TestSetup.CreateMixer(bank));

        await _runtime.SetSubValueAsync(bank.Id, 1, 50);
        await _runtime.SetSubValueAsync(bank.Id, 3, 99);
        await TestSetup.WaitForAsync(() => _runtime.GetControlState(bank.Id)?.SubValues?[3] == 99);

        var state = _runtime.GetControlState(bank.Id)!;
        Assert.Equal([0, 50, 0, 99], state.SubValues);
    }

    [Fact]
    public async Task Reregistering_mixer_keeps_values_of_surviving_controls()
    {
        var fader = TestSetup.CreateControl(BuiltInControlTypes.Fader, new FaderSettings());
        var mixer = TestSetup.CreateMixer(fader);
        _runtime.RegisterMixer(mixer);

        await _runtime.SetControlValueAsync(fader.Id, 77);
        await TestSetup.WaitForAsync(() => _runtime.GetControlState(fader.Id)?.Value == 77);

        // Re-register (e.g. after saving in designer) - value must survive.
        _runtime.RegisterMixer(mixer);
        Assert.Equal(77, _runtime.GetControlState(fader.Id)!.Value);
    }

    [Fact]
    public async Task Event_monitor_ring_buffer_is_bounded_and_clearable()
    {
        var fader = TestSetup.CreateControl(BuiltInControlTypes.Fader, new FaderSettings());
        _runtime.RegisterMixer(TestSetup.CreateMixer(fader));

        using var buffer = new EventMonitorBuffer(_runtime, Options.Create(new VirtualMixerOptions
        {
            EventMonitorCapacity = 10
        }));

        for (var i = 1; i <= 25; i++)
        {
            await _runtime.SetControlValueAsync(fader.Id, i);
        }

        await TestSetup.WaitForAsync(() => _runtime.GetControlState(fader.Id)?.Value == 25);
        await TestSetup.WaitForAsync(() => buffer.GetEntries().Count > 0);

        Assert.True(buffer.GetEntries().Count <= 10);

        buffer.Clear();
        Assert.Empty(buffer.GetEntries());
    }
}
