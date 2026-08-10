# Changelog

## 1.0.0-rc1 — 2026-08-10

### Core workflows

- Unified version-4 `.vns` projects across MIDI, audio, stems, Shawzin, Creator and Mandachord data.
- MIDI import/export and playback core; Shawzin codec, arrangement, preview and multi-instrument splitting.
- Audio Lab import/waveform/playback plus optional Demucs and Basic Pitch worker integration.
- Creator take planning and Mandachord generation/edit/preview/export.

### Release readiness

- Added separate autosave recovery snapshots, startup recovery detection, Recent Projects and bounded settings validation.
- Added English/German UI resources and System/Light/Dark theme selection.
- Added Dependency Center diagnostics with text/JSON export, manual path configuration and no silent installation.
- Added Shawzin code roundtrip inspection, mapping test sequences and local validation records.
- Hardened `.vns` containers against unsafe/absolute paths, duplicate entries, symlinks, excessive size and suspicious compression.
- Added graceful cancellation/cleanup, bounded rotating logs and centralized `1.0.0-rc1` build version.
- Added Windows/Linux portable packaging scripts, package-content validation, CI NuGet caching and startup smoke probe.
- Added README, user/quick-start/AI/packaging documentation, release test plan, known issues and third-party notices.
