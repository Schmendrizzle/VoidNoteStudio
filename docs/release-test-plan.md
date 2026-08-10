# VoidNote Studio 1.0.0-rc1 Release Acceptance Test Plan

Record OS, build hash, package name, result and notes for every run. Use generated/public-domain fixtures only. Never run real GameBridge input without deliberate user authorization and a safe text target.

## Windows x64 and Linux x64 common checks

- [ ] Extract the portable package into a new writable folder; confirm no logs, models, test output, `.git`, `bin` or `obj` content.
- [ ] Run the native app host (`VoidNote.App.exe --version` / `./VoidNote.App --version`); confirm `1.0.0-rc1`. Do not use `dotnet VoidNote.App.dll` as proof of self-contained packaging.
- [ ] Start the GUI with no optional dependencies; Welcome/Project appears and no account/cloud is requested.
- [ ] Switch English/German and System/Light/Dark, restart, and confirm selection plus readable focus indicators/high contrast.
- [ ] Keyboard-tab through Project, Audio, Shawzin, Creator, Mandachord and Settings; verify focus order, labels, tooltips and 150–200% UI scaling.
- [ ] Create, save, close and open a project. Confirm title, path and dirty indicator.
- [ ] Open v1, v2 and v3 fixtures; save copies; compare all expected tracks/assets and verify version-labelled backups. Reopen v4.
- [ ] Import a multi-track MIDI with tempo changes; analyze and arrange; preview and validate its Shawzin code.
- [ ] Split to 2/3/4 Shawzins; inspect individual codes and Creator take preparation.
- [ ] Paste known valid/invalid Shawzin codes; decode, validate, re-encode and inspect differences/timing.
- [ ] Generate a Shawzin mapping sequence; save confirmed and unconfirmed local records.
- [ ] Import WAV. With FFmpeg installed, import MP3 and FLAC and generate waveforms.
- [ ] With AI missing, verify clear unavailable states and continued normal operation.
- [ ] With an explicitly prepared AI environment, run `python scripts/smoke-tests.py engines`; optionally authorize Demucs model-cache behavior with the documented flag.
- [ ] Transcribe a tonal stem, inspect confidence, edit/arrange it and preserve provenance.
- [ ] Generate Mandachord candidates, edit each layer, preview, accept, save and reopen.
- [ ] Create Creator session, dry run, partial take, retake, notes, JSON/CSV/WAV export and emergency stop.
- [ ] Enable 1-minute autosave, make a change, wait, terminate abnormally in a controlled test, restart, recover, verify original unchanged, then explicitly save recovered work.
- [ ] Discard a separate recovery snapshot and verify only recovery files disappear.
- [ ] Run Dependency Diagnostics; export text/JSON and review it for private content.
- [ ] Run MP3/FLAC/FFplay capability smoke: `python scripts/smoke-tests.py ffmpeg`.
- [ ] Cancel waveform, FFmpeg, separation and transcription operations; confirm no worker/FFmpeg zombie remains.
- [ ] Exercise a large MIDI, long WAV/FLAC/MP3, many tracks/stems/ensembles, large waveform cache and creator/mandachord project; observe UI responsiveness and memory.
- [ ] Close during active playback/job; verify job cancellation, worker stop, audio stop, GameBridge release/disarm, autosave completion, temp cleanup and settings persistence.

## GameBridge manual workflow

- [ ] Run Diagnostic Input with a profile; confirm no real keys appear in another application.
- [ ] Verify Windows capability reports SendInput. On Linux verify X11/XTest or an explicit Wayland-unavailable result.
- [ ] Optional real input test: read the third-party warning, use a disposable local text editor—not gameplay—as target, explicitly Arm, play only the short generated music test, then Stop/Emergency Stop and verify no held key remains.
- [ ] If later testing in Warframe is chosen by the user, restrict it to the intended music function. Do not test combat, movement, missions, resources or AFK behavior. Record that Digital Extremes does not endorse VoidNote and risk remains with the user.

## Platform-specific packaging

- [ ] Windows: on a machine without a global .NET runtime, ZIP extracts and `VoidNote.App.exe` starts; document only the expected unsigned SmartScreen warning.
- [ ] Linux: on a representative x64 desktop without a global .NET runtime, the extracted `VoidNote.App` starts and retains its executable permission.
- [ ] Ubuntu: tar.gz extracts as user, executable permission is correct, app starts in a desktop session.
- [ ] Linux X11 and Wayland differences match UI and documentation; no root/uinput workaround.
- [ ] GitHub Actions Windows and Ubuntu jobs pass restore, build, tests, self-contained publish validation and native app-host `--version` startup probe.
