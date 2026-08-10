# First Release Security Review

This is a focused release review, not a complete security audit or guarantee.

## Reviewed boundaries

- Repository searches found no DLL/process injection, process memory read/write, Warframe hooks, packet manipulation or anti-cheat bypass. Windows GameBridge uses documented `SendInput`; Linux uses X11/XTest. Both remain behind interfaces, explicit arm/focus checks and release-all cleanup.
- External processes use `ProcessStartInfo.ArgumentList`, `UseShellExecute=false` and bounded cancellation. Worker/FFmpeg process trees are killed on cancellation/failure; no shell-composed user arguments were found.
- AI job directories require a VoidNote marker and an owned-root prefix before recursive deletion.
- `.vns` load now rejects rooted paths, `..`, drive/colon paths, duplicate entry names, Unix symlinks, excessive entry/expanded sizes and suspicious compression ratios. Embedded assets extract only to generated project/GUID names and are copied with an explicit byte limit.
- Settings and project writes use temporary files before replacement. Migration backups are version-labelled and existing backups are not overwritten.
- Diagnostics are local and export capability metadata rather than song/audio content. Logs are local, rotated at 5 MiB and retained for 14 days by default.

## Residual risk

- ZIP size/compression heuristics reduce common decompression-bomb risk but are not a formal resource sandbox.
- Configured executable/worker paths are trusted local user choices. They are not downloaded or installed by VoidNote.
- Dependency and model supply-chain security remains the user's/package distributor's responsibility.
- External `.vns`, MIDI, audio and worker results must continue to be treated as untrusted inputs as formats evolve.
