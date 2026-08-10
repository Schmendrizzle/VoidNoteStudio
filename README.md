# VoidNote Studio

VoidNote Studio is an offline-first Windows and Linux music workspace for importing audio and MIDI, arranging music for Shawzin, preparing multi-Shawzin creator takes, and producing Mandachord patterns. The 1.0.0-rc1 build is a release candidate intended for practical validation, not a claim that automatic transcription or every in-game mapping is perfect.

## Features

- Versioned `.vns` projects with MIDI, audio, stems, Shawzin, Creator and Mandachord data on one master timeline.
- MIDI import/export, deterministic Shawzin arrangement, song-code roundtrip validation and multi-Shawzin splitting.
- WAV import without external tools; optional FFmpeg-based MP3/FLAC decode and FFplay preview.
- Optional local Demucs stem separation and Basic Pitch transcription through an isolated Python worker.
- Creator take planning, sync exports and diagnostic-only GameBridge testing.
- Autosave/recovery snapshots, recent projects, English/German resources, dark/light/system themes and local dependency diagnostics.

## Screenshots

Screenshots will be added after the RC visual review. The current RC contains Project, Audio Lab, Shawzin Studio, Creator Mode, Mandachord Studio and Settings workspaces.

## Windows

Use the `win-x64` portable ZIP, extract it to a writable folder and run `VoidNote.App.exe`. The official RC package includes the required .NET 10 runtime; no separate .NET installation is required. Unsigned builds can trigger Microsoft SmartScreen; review the source and package contents before choosing to run them.

## Linux

Use the `linux-x64` tarball, extract it and run `VoidNote.App` from a desktop session. The executable bit and required .NET 10 runtime files are included; no separate .NET installation is required. Composition, preview, conversion and export work under X11 and Wayland. Optional real GameBridge input is X11-only; Wayland intentionally reports it unavailable.

## Requirements

- Windows x64 or desktop Linux x64.
- FFmpeg/ffprobe/ffplay only for MP3, FLAC and live FFplay output.
- Python 3.10+ only for optional AI features.

See [AI setup](docs/ai-setup.md) and the in-app Dependency Center for explicit setup and diagnostics. VoidNote never installs packages or downloads AI dependencies silently.

## Basic usage

Start on the Project page, create or open a `.vns` project, then import MIDI in Shawzin Studio or audio in Audio Lab. Analyze before arranging, preview the result, validate the generated code, and save the project explicitly. See [Quick Start](docs/quick-start.md) and the [User Guide](docs/user-guide.md).

## Safety and Warframe disclaimer

VoidNote Studio is an independent community project and is not affiliated with or endorsed by Digital Extremes. Use of third-party software with Warframe is at your own risk. VoidNote makes no claim that real input playback is approved, risk-free or “ban-safe”. The GameBridge uses only normal, explicitly armed OS keyboard input for the music function; it does not read or modify the Warframe process, memory, files or network traffic. Diagnostic mode sends no real keys.

## Build from source

Install the SDK selected by `global.json`, then run:

```text
dotnet restore VoidNoteStudio.sln
dotnet build VoidNoteStudio.sln --configuration Release --no-restore
dotnet test VoidNoteStudio.sln --configuration Release --no-build --no-restore
```

Reproducible portable packaging is documented in [Release Packaging](docs/release-packaging.md) and automated by `scripts/build-release.ps1` and `scripts/build-release.sh`.

## Development

The solution uses C#, .NET, Avalonia, MVVM and modular Clean Architecture. Read `AGENTS.md`, `VOIDNOTE_SPEC.md` and [Architecture](docs/architecture.md) before architectural work. Long-running operations must remain cancellable and optional dependencies must never block normal startup.

## License

VoidNote Studio source is licensed under the [MIT License](LICENSE). Dependency licenses and optional-tool notices are listed in [Third-Party Notices](THIRD_PARTY_NOTICES.md).
