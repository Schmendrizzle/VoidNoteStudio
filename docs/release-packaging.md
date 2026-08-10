# Release packaging

VoidNote Studio 1.0.0-rc1 uses **framework-dependent portable packages** for Windows x64 and Linux x64. This keeps packages smaller and avoids redistributing the .NET runtime; users must install the .NET 10 runtime. A later release may add self-contained variants after size and servicing review.

## Reproducible process

From a clean checkout with the SDK in `global.json`:

- Windows PowerShell: `./scripts/build-release.ps1`
- Linux/macOS shell: `./scripts/build-release.sh`

The scripts clean, restore, build Release, run all normal tests, publish both RIDs, add README/license/notices, validate package contents, start the published managed entry point with `--version`, then produce:

- `artifacts/release/VoidNote-Studio-1.0.0-rc1-win-x64.zip`
- `artifacts/release/VoidNote-Studio-1.0.0-rc1-linux-x64.tar.gz`

Package validation rejects logs, test output, model files, virtual environments, `.git`, `bin`, `obj`, common IDE files and likely personal absolute paths. AI models and FFmpeg are not bundled.

## Windows notes

Extract the portable ZIP before running. No installer or signing certificate is required to build the RC. An installer is deliberately deferred until upgrade/uninstall behavior can be tested. Unsigned executables may produce SmartScreen reputation warnings; do not imply signing or Microsoft approval.

## Linux notes

The tarball requires no root installation. Avalonia needs a normal desktop environment. GameBridge real input supports X11/XTest only; Wayland remains diagnostic/export-only. AppImage is not produced in rc1 because its runtime bundling and distribution compatibility have not yet received sufficient cross-distribution validation.

## CI

GitHub Actions builds/tests on `windows-latest` and `ubuntu-latest`, caches NuGet packages by SDK/package/project inputs, performs a framework-dependent publish, validates the staging directory and runs `VoidNote.App.dll --version`. CI does not download AI models, send keyboard input, access audio devices or perform real engine tests.

## Backup retention

Migration creates at most one version-labelled backup beside a project (`.v1.bak`, `.v2.bak`, `.v3.bak`) before the first current-format overwrite. Existing backups are never replaced. Format v4 is current; future migrations must use a new version-labelled backup and keep the bounded settings retention (default three generations). Autosaves live separately under application data and are removed only by explicit discard or future retention cleanup—not mixed with migration backups.
