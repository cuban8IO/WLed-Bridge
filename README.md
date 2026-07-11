# WLed Midi Controller

A Blazor Server app that bridges MIDI controllers to [WLED](https://kno.wled.ge/) LED installations. MIDI controllers connect through swappable driver + transport abstractions and get mapped to a virtual mixer, which will eventually drive WLED devices.

Built in Clean Architecture so every layer - MIDI transport, controller driver, persistence, UI - can be swapped independently. See [`CLAUDE.md`](CLAUDE.md) for the full architecture breakdown, hardware protocol notes, and known gotchas.

## Status

- ✅ Clean Architecture skeleton (Domain / Application / Infrastructure / Web)
- ✅ Bidirectional MIDI transport (NAudio-backed, swappable)
- ✅ Controller driver framework, with a hardware-verified **Akai APC40 mkII** driver (pads, faders, buttons, encoders, LED/ring feedback)
- ✅ EF Core + SQLite persistence (Code First, Fluent API)
- ✅ Device Test page (`/devices/test`) for bidirectional hardware testing
- ⬜ WLED device management UI
- ⬜ Virtual mixer UI
- ⬜ MIDI-control → WLED-action mapping engine

## Getting started

Requires .NET 10 SDK.

```bash
dotnet build wledBridge.slnx
dotnet run --project src/wledBridge.Web
```

The app applies pending EF Core migrations automatically on startup (SQLite file created next to the Web project). Open `http://localhost:5073`, then `/devices/test` to connect a MIDI controller.

## Supported controllers

| Controller | Status |
|---|---|
| Akai APC40 mkII | Pads, faders, buttons, encoders (absolute + relative), LED/ring feedback verified against real hardware |

Driver implementation: [`src/wledBridge.Infrastructure/Controllers/Apc40Mk2Driver.cs`](src/wledBridge.Infrastructure/Controllers/Apc40Mk2Driver.cs), based on Akai's official protocol document, included at [`docs/APC40Mk2_Communications_Protocol_v1.2.pdf`](docs/APC40Mk2_Communications_Protocol_v1.2.pdf).

New controller models are added by implementing `IControllerDriver` and registering it in `ControllerDriverFactory` - nothing else in the app needs to change.
