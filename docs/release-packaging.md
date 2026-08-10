# Release packaging

VoidNote Studio 1.0.0-rc1 uses **self-contained, multi-file .NET 10 portable packages** for Windows x64 and Linux x64. The official archives include the .NET runtime and do not require a global .NET installation.

## Publish configuration

The distributable configurations are isolated in `src/VoidNote.App/Properties/PublishProfiles/win-x64-portable.pubxml` and `linux-x64-portable.pubxml`. Both set:

- `SelfContained=true` and `UseAppHost=true`.
- an explicit `RuntimeIdentifier` (`win-x64` or `linux-x64`).
- `PublishSingleFile=false`, `PublishTrimmed=false` and `PublishReadyToRun=false`.
- release symbols off.

Normal `dotnet build` and `dotnet test` remain RID-neutral development/CI builds. Only release packaging and the CI packaging smoke step select a portable publish profile.

Single-file publishing is deliberately disabled for RC1. Avalonia uses native platform assets, and VoidNote ships a Python worker as a visible file resolved below `AppContext.BaseDirectory`. Optional FFmpeg, FFplay and Python executables are configured paths or normal `PATH` commands, while GameBridge selects its OS adapter at runtime. The multi-file layout preserves these behaviors, avoids extraction-specific paths and keeps native runtime files directly auditable. FFmpeg, Python itself, AI packages and AI models are not bundled.

## Reproducible process

From a clean checkout with the SDK in `global.json`:

- Windows PowerShell: `./scripts/build-release.ps1`
- Linux shell: `./scripts/build-release.sh`

The scripts clean, restore, build Release, run all normal tests, publish both RIDs from their official profiles, add README/license/notices, validate package contents, run the native app host with `--version` when it matches the build host, then produce:

- `artifacts/release/VoidNote-Studio-1.0.0-rc1-win-x64.zip`
- `artifacts/release/VoidNote-Studio-1.0.0-rc1-linux-x64.tar.gz`

Validation rejects logs, test output, model files, virtual environments, `.git`, `bin`, `obj`, common IDE files and likely personal absolute paths. It also requires the platform app host, CoreCLR, HostFXR, HostPolicy and `System.Private.CoreLib`, checks the RID-specific .NET runtime pack in `VoidNote.App.deps.json`, and rejects runtime configs that request a globally installed shared framework. The small .NET-only packaging helper also creates the Linux tarball with a deterministic executable mode for `VoidNote.App` and verifies that mode after writing, including during cross-publish on Windows. These structural and metadata checks are the automated proof of self-contained publishing; a `--version` run alone is only a startup smoke test.

## Windows notes

Extract the complete portable ZIP before running. No installer, global .NET runtime or signing certificate is required. An installer is deliberately deferred until upgrade/uninstall behavior can be tested. Unsigned executables may produce SmartScreen reputation warnings; do not imply signing or Microsoft approval.

## Linux notes

The tarball requires no root or .NET installation. Avalonia still needs a normal desktop environment and compatible system graphics/windowing libraries. GameBridge real input supports X11/XTest only; Wayland remains diagnostic/export-only. AppImage is not produced in RC1 because its cross-distribution behavior has not been sufficiently validated.

## CI

GitHub Actions keeps the ordinary Release build/test stage separate, then publishes only the host RID with the same official self-contained profile. It performs structural/metadata validation and invokes the native app host with `--version`. CI does not download AI models, send keyboard input, access audio devices or perform real engine tests.

## Backup retention

Migration creates at most one version-labelled backup beside a project (`.v1.bak`, `.v2.bak`, `.v3.bak`) before the first current-format overwrite. Existing backups are never replaced. Format v4 is current; future migrations must use a new version-labelled backup and keep the bounded settings retention (default three generations). Autosaves live separately under application data and are removed only by explicit discard or future retention cleanup—not mixed with migration backups.
