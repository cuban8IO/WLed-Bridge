using wledBridge.VirtualMixer.Services;
using wledBridge.VirtualMixer.State;
using Xunit;

namespace wledBridge.VirtualMixer.Tests;

public class ManagerTests
{
    private static (VirtualMixerManager Manager, InMemoryStore Store, MixerRuntimeState Runtime) Create()
    {
        var serializer = TestSetup.CreateSerializer();
        var store = new InMemoryStore(serializer);
        var runtime = new MixerRuntimeState(TestSetup.CreateRegistry());
        var manager = new VirtualMixerManager(store, serializer, runtime);
        return (manager, store, runtime);
    }

    [Fact]
    public async Task Activation_is_exclusive()
    {
        var (manager, _, _) = Create();
        var a = await manager.CreateMixerAsync("A");
        var b = await manager.CreateMixerAsync("B");

        await manager.ActivateMixerAsync(a);
        await manager.ActivateMixerAsync(b);

        var mixers = await manager.GetMixersAsync();
        Assert.Single(mixers, m => m.IsActive);
        Assert.True(mixers.Single(m => m.Id == b).IsActive);
    }

    [Fact]
    public async Task Created_mixer_always_has_a_default_group()
    {
        var (manager, _, _) = Create();
        var id = await manager.CreateMixerAsync("Neu");
        var mixer = await manager.GetMixerAsync(id);

        Assert.NotNull(mixer);
        Assert.Single(mixer.Groups);
    }

    [Fact]
    public async Task Export_import_creates_inactive_copy_with_new_ids()
    {
        var (manager, _, _) = Create();
        var id = await manager.CreateMixerAsync("Original");
        var mixer = await manager.GetMixerAsync(id);
        mixer!.Groups[0].Controls.Add(TestSetup.CreateControl(
            Controls.BuiltInControlTypes.Button, new Models.ButtonSettings()));
        await manager.SaveMixerAsync(mixer);
        await manager.ActivateMixerAsync(id);

        var json = await manager.ExportMixerAsync(id);
        var importedId = await manager.ImportMixerAsync(json);
        var imported = await manager.GetMixerAsync(importedId);

        Assert.NotNull(imported);
        Assert.NotEqual(id, importedId);
        Assert.False(imported.IsActive);
        Assert.NotEqual(
            mixer.Groups[0].Controls[0].Id,
            imported.Groups[0].Controls[0].Id);
    }

    [Fact]
    public async Task Deleting_active_mixer_raises_active_changed_with_null()
    {
        var (manager, _, _) = Create();
        var id = await manager.CreateMixerAsync("A");
        await manager.ActivateMixerAsync(id);

        Events.MixerActivatedEventArgs? received = null;
        manager.ActiveMixerChanged += (_, e) => received = e;

        await manager.DeleteMixerAsync(id);

        Assert.NotNull(received);
        Assert.Null(received.ActiveMixer);
        Assert.Null(await manager.GetActiveMixerAsync());
    }

    [Fact]
    public async Task Loading_normalizes_multiple_persisted_active_mixers()
    {
        var serializer = TestSetup.CreateSerializer();
        var store = new InMemoryStore(serializer);

        var m1 = TestSetup.CreateMixer();
        m1.IsActive = true;
        var m2 = TestSetup.CreateMixer();
        m2.IsActive = true;
        await store.SaveMixerAsync(m1);
        await store.SaveMixerAsync(m2);

        var runtime = new MixerRuntimeState(TestSetup.CreateRegistry());
        var manager = new VirtualMixerManager(store, serializer, runtime);

        var mixers = await manager.GetMixersAsync();
        Assert.Single(mixers, m => m.IsActive);
    }
}
