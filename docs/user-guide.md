# VoidNote Studio User Guide

## Portable installation

Extract the complete official `win-x64` ZIP or `linux-x64` tarball before starting VoidNote. These packages are self-contained and include the required .NET 10 runtime, so no global .NET installation is required. Keep all extracted files together; the runtime libraries and the bundled Python worker script are resolved from the package directory. FFmpeg and Python remain optional external tools for the features described below.

## Projects and recovery

Create or open projects from **Project**. `.vns` files are versioned ZIP containers; do not edit their contents manually. The header shows the open project, unsaved state and background jobs. Saving is atomic. Autosave uses separate recovery snapshots under application data and never replaces the normal `.vns` file. On the next start, a newer snapshot shows its project and time. **Recover** opens it as unsaved work; **Ignore / Discard** removes only that recovery snapshot.

Recent Projects stores name, path and last-opened time locally. A missing path is labelled and is never moved, recreated or deleted automatically.

## MIDI import

Open **Shawzin Studio**, choose **Open MIDI**, and select a Format 0/1 PPQ MIDI file. Tracks, notes, tempo and time signatures are projected onto the master timeline. SMPTE time division and several advanced MIDI event types are not normalized in this RC; see Known Issues.

Select a track before analysis. Compatibility reports direct notes, octave-fixable notes, unsupported notes, density, timing and chord conflicts. Automatic arrangement changes are listed rather than hidden.

## Shawzin code import, validation and export

The validation area accepts the supported Warframe Recorded Song V1 code. **Decode / Validate / Re-Encode** shows validity, instrument profile, event count, duration, timing spacing, canonical output and differences. Generated track codes can be copied after validation. The code format does not contain an instrument or tuning, so select the intended instrument profile separately.

## Multi-Shawzin

Choose two to four Shawzins and a split strategy, then split the selected normalized MIDI track. Every output track keeps an independent instrument, scale, transposition, compatibility report and code. Review note loss, balance and continuity. Creator Mode can prepare separate takes from the ensemble.

## Audio import and playback

Audio Lab imports WAV directly. MP3 and FLAC require configured FFmpeg and ffprobe. Source files are read-only; clips, trim, gain, mute, solo, regions and waveform cache are non-destructive. FFplay live output targets the system default device in this RC. Use Cancel for long probe, import or waveform operations.

## Stem separation

Install Demucs manually and configure the Python/worker paths. Select an audio source or region, choose compute preference, then run Separation. Results are new derived sources and a `StemSet`; the original remains untouched. Removing a set is undoable at the project-model level. Preview synchronization is not a sample-accurate DAW mix.

## Audio-to-MIDI

Select audio or a stem, configure mode, quantization and minimum confidence, then transcribe. Basic Pitch output is a proposal. Review low-confidence, octave, onset and polyphony errors before arrangement. Drum stems are not supported by the pitch transcription adapter.

## Mandachord

Mandachord Studio consumes normalized MIDI or transcription events and creates deterministic Faithful, Recognizable, Gameplay, Rhythm Focus or Melody Focus candidates. Compare scores, edit steps, preview the synthetic loop and accept the desired candidate into the project. The format is a VoidNote representation, not a native Warframe share-code claim.

## Creator Mode

Create a session from selected ensemble tracks. For each take, review preparation, count-in, sync point, source range, checklist and code. Dry Run changes no take and sends no real input. Mark attempts complete or create a retained retake. JSON, CSV and synthetic sync-WAV exports help align external video editing. OBS, NLE integration and automatic video work are intentionally out of scope.

## GameBridge

Diagnostic mode validates profiles and timing without real keys. Real input remains disarmed until the third-party warning is acknowledged and **Arm** is deliberately selected. Focus loss, errors, stop, emergency stop and application shutdown release held keys and disarm. Windows uses documented `SendInput`; Linux supports X11/XTest only. Wayland has no privileged workaround.

VoidNote Studio is independent and not affiliated with or endorsed by Digital Extremes. Third-party software is used with Warframe at the user's own risk. No claim of approval or ban safety is made. Never use the music bridge for combat, movement, missions, resources or AFK gameplay.

## Dependency Center

Settings contains paths for FFmpeg, FFplay, Python and the worker. Save and restart after path changes, then Refresh/Recheck. Diagnostics reports .NET, OS, FFmpeg, FFplay, Python, Demucs, Basic Pitch, audio backend, GameBridge capability, write permission and temporary directory. Text/JSON exports omit project content; review paths before sharing.

## Appearance, language and accessibility

Choose System, Light or Dark, plus English or German. These settings apply reliably after restart. Standard controls preserve keyboard focus indicators and scalable layout. Tab through controls; scroll dense workspaces at larger text/UI scaling. Tooltips identify core shortcuts.

## Keyboard shortcuts

- `Ctrl+S`: save the current project (opens a destination picker if needed).
- `Ctrl+O`: open a project.
- `Space`: play/pause where the active transport exposes it; avoid triggering while editing text.
- `Ctrl+Z` / `Ctrl+Y`: Undo/Redo in editors backed by the project history.
- `Delete`: delete the active editor selection, never text outside the focused control.
- `R`: record only where a recording control is active; this RC has no global recording hotkey.
- Stop is available as an explicit transport button; Emergency Stop is always visible near GameBridge controls.

## Troubleshooting

- **MP3/FLAC unavailable:** configure FFmpeg and ffprobe, restart, then recheck.
- **No live audio:** configure FFplay; offline synthetic WAV generation still works.
- **AI unavailable:** verify the exact Python interpreter contains the package and Worker path exists.
- **Wayland GameBridge unavailable:** use diagnostic mode/export or an X11 session; no root workaround is provided.
- **Project recovery shown:** compare project and timestamp, recover to unsaved state, then save explicitly to a chosen `.vns` file.
- **Project rejected:** external `.vns` files are untrusted; invalid paths, duplicate entries, symlinks, excessive sizes and suspicious compression are rejected.
