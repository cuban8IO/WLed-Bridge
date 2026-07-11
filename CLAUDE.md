# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build the whole solution
dotnet build wledBridge.slnx

# Run the web app (applies pending EF Core migrations automatically on startup)
dotnet run --project src/wledBridge.Web

# Add a new EF Core migration (must pass both --project and --startup-project)
dotnet ef migrations add <Name> \
  --project src/wledBridge.Infrastructure/wledBridge.Infrastructure.csproj \
  --startup-project src/wledBridge.Web/wledBridge.Web.csproj \
  --output-dir Persistence/Migrations
```

There is no test project yet. `NuGet.config` at the repo root pins package restore to `nuget.org` only (`<clear/>` + one source) - the machine's globally configured company feed 401s on this network and would otherwise break `dotnet restore`/`dotnet add package` for anyone else on the same machine/account.

The dev server for the `run` skill / preview tooling is `wledBridge.Web` (see `.claude/launch.json`), serving on `http://localhost:5073`. Device Test page: `/devices/test`.

## Architecture

Clean Architecture, four projects under `src/`, dependency direction `Web → Infrastructure → Application → Domain`. Root namespace is `wledBridge` (lowercase, intentional - not a typo).

- **wledBridge.Domain** - no dependencies. Entities: `WledDevice`/`WledState`, and the controller-mapping model `ControllerDevice`, `VirtualMixer`/`VirtualControl`, `ControlMapping`, `ControlType` enum (Pad/Encoder/Fader/Button).
- **wledBridge.Application** - abstractions + orchestration only, no concrete implementations.
  - `Abstractions/Midi`: `IMidiPortFactory`, `IMidiInputPort`, `IMidiOutputPort`, `MidiMessage` (raw status/data1/data2) - bidirectional MIDI transport, swappable (currently NAudio-backed).
  - `Abstractions/Controllers`: `IControllerDriver`, `IControllerDriverFactory`, `ControlDescriptor`, `ControlValueChangedEventArgs`, `ControllerColor`, `EncoderRingStyle` - the per-controller-model driver contract. New controller hardware = new `IControllerDriver` implementation registered in the factory; nothing else changes.
  - `Controllers/ControllerSession` + `ControllerSessionManager`: opens MIDI ports via the port factory, creates a driver via the driver factory, wires `IMidiInputPort.MessageReceived/SysExReceived` into `driver.HandleMessage/HandleSysEx`. This is the runtime "glue" - one active session at a time (singleton), used by the Device Test page.
  - `Abstractions/Persistence/IApplicationDbContext`: exposes `DbSet<T>` per aggregate; implemented by the EF Core context in Infrastructure.
- **wledBridge.Infrastructure** - concrete adapters.
  - `Midi/NAudio*`: `IMidiPortFactory`/`IMidiInputPort`/`IMidiOutputPort` over NAudio (Windows winmm, so Windows-only).
  - `Controllers/Apc40Mk2Driver` + `Apc40ColorPalette` + `ControllerDriverFactory`: see "APC40 mkII driver" below. New drivers get added to `ControllerDriverFactory`'s dictionary.
  - `Wled/WledHttpClient`: talks to WLED's `/json/state` HTTP API.
  - `Persistence/`: `WledBridgeDbContext` (Code First, one `IEntityTypeConfiguration<T>` per aggregate in `Persistence/Configurations`), SQLite. Migrations applied automatically at startup in `Program.cs` (`dbContext.Database.Migrate()`), not via CLI on deploy.
- **wledBridge.Web** - Blazor Server + MudBlazor, composition root (`Program.cs` wires `AddApplication()`/`AddInfrastructure()`).
  - Interactivity is **global**, declared once as `<Routes @rendermode="InteractiveServer" />` in `App.razor`. Do **not** add a per-page `@rendermode` - a page-level render mode creates a separate render tree from `MainLayout`, and `MudPopoverProvider` (declared once in `MainLayout.razor`) then isn't reachable from that page, silently breaking every popover-based component (`MudSelect`, `MudColorPicker`, etc.) with no visible error beyond a console log.
  - `Theme/DarkTheme.cs`: dark MudBlazor theme, condensed font (Oswald), Beatport-inspired accent color `#01FF95` (sourced via web search, not scraped from the live site - treat as approximate if it ever needs revisiting).
  - `Components/Pages/DeviceTest.razor` (`/devices/test`) is the primary hardware-facing tool: connect to a controller (driver + MIDI in/out), live raw MIDI monitor, live parsed control-event log, pad/scene color tester with a themed palette, encoder LED-ring tester (Single/Fill/Pan style), button LED tester, physical-to-virtual-mixer mapping table, and a device reset button. Also has a top-right app-bar menu for restarting/exiting the whole process (`Environment.Exit(0)` - see gotcha below).
  - EF Core `IApplicationDbContext` is registered `Scoped`; only ever touched from Blazor-invoked handlers (button clicks, dropdown changes), never from the raw MIDI callback thread, to avoid concurrent access to the same `DbContext` instance.

### APC40 mkII driver

`Apc40Mk2Driver` is built from Akai's official **"APC40 Mk2 Communications Protocol v1.2"** PDF (`docs/APC40Mk2_Communications_Protocol_v1.2.pdf`), verified live against real hardware over the course of this project - trust the code/PDF over intuition here, several plausible-looking guesses turned out wrong:

- Device is put into **Ableton Live Mode** (SysEx mode byte `0x41`) on attach. **Do not** try Alternate Ableton Live Mode (`0x42`) - it was tested and breaks LED ring feedback entirely (device stops auto-rendering ring position), it's not just "an alternate style".
- Pads (and Scene Launch, which uses the identical mechanism): fixed **channel 0**, `note = row*8 + col` (row 0 = bottom, row 4 = top). Color is velocity-encoded via the exact 128-entry palette in `Apc40ColorPalette.cs`, channel 0 = "Primary Color" per the protocol's RGB LED type table.
- Track-scoped controls (faders, activator/solo/record-arm/select/clip-stop/crossfader-assign): **channel = column** (0-7), fixed note/CC per function.
- Device Encoders and Pan/Track Encoders: **absolute** position (per protocol's "Type CC1"), even though they're endless-turning encoders - the firmware tracks and saturates the position internally. Only **Cue Level** and **Tempo Knob** are relative (protocol's "Type CC2": data2 `1-63` = +N, `64-127` = -(128-data2)).
- Encoder **LED ring style** (Single/Volume-fill/Pan) is a **separate CC per encoder** (`0x18-0x1F` device, `0x38-0x3F` track knobs; value `1`/`2`/`3`), sent independently of the position value - the device renders its own tracked position once a style is set, so never push a synthetic value just to "activate" a style (this caused a real bug: forcing 50%/0% values on every connect/style-change, and Volume-style at value 0 renders as fully blank while Single-style at 0 shows one LED, making connect and reset look inconsistent until this was fixed).
- `Reset()` intentionally leaves rings on **Single Position at value 0** (one LED lit), not ring-type-off - a fully-off ring type requires reselecting a style before anything shows again, which was reported as a bug.
- Several buttons (Play/Record/Session Record/Pan/Sends/User/etc.) get their LED lit only if the **host** explicitly sends it - the device does not self-illuminate button LEDs in Ableton Live Mode. `DeviceTest.razor`'s `OnControlChanged` echoes button presses back as `SetButtonLed` calls for this reason.

### Known gotchas already solved (don't reintroduce)

- **Shutdown**: use `Environment.Exit(0)`, not `IHostApplicationLifetime.StopApplication()` - the latter waits for open connections (including the SignalR circuit the shutdown request came in on) to drain and can hang indefinitely.
- **MudPopoverProvider**: see the Web bullet above - one global render mode, not per-page.
- **File locks during `dotnet build`**: a previously-started `dotnet run`/preview process holds the output DLLs/EXE open; stop it first (or check `dotnet ef`/build errors mentioning `wledBridge.Web.exe`/`.dll` locked by another process).

## Status

Implemented: Clean Architecture skeleton, bidirectional MIDI transport, controller driver framework with a verified APC40 mkII driver, EF Core/SQLite persistence, Device Test page.

Not yet built: WLED device management UI (entity + HTTP client exist, no CRUD UI), a dedicated virtual-mixer UI (schema + a basic physical-to-virtual mapping table exist on the Device Test page, but no standalone mixer view), and the actual MIDI-control → WLED-action mapping/routing engine.
