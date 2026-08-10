# Third-Party Notices

VoidNote Studio 1.0.0-rc1 uses or interoperates with the components below. This summary is not a replacement for each project's license. Release maintainers must retain the license files shipped by any redistributed binary.

## Bundled .NET dependencies

| Component | Use | License |
| --- | --- | --- |
| [Avalonia](https://github.com/AvaloniaUI/Avalonia) | Cross-platform UI, desktop host, Fluent theme | MIT |
| [DryWetMIDI](https://github.com/melanchall/drywetmidi) | Standard MIDI File import/export | MIT |
| [Microsoft.Extensions.DependencyInjection](https://github.com/dotnet/runtime) | Dependency injection | MIT |
| [Microsoft.Extensions.Logging](https://github.com/dotnet/runtime) | Logging abstractions and console logging | MIT |

## Test-only dependencies

| Component | Use | License |
| --- | --- | --- |
| [xUnit.net](https://github.com/xunit/xunit) | Automated tests | Apache License 2.0 |
| [Microsoft.NET.Test.Sdk / VSTest](https://github.com/microsoft/vstest) | Test host | MIT |

## Optional external dependencies (not bundled)

| Component | Use | License note |
| --- | --- | --- |
| [FFmpeg](https://ffmpeg.org/legal.html) | MP3/FLAC decode, probing and FFplay output | FFmpeg is primarily LGPL 2.1-or-later; a particular build may be GPL depending on enabled components. VoidNote does not bundle an FFmpeg build. |
| [Demucs](https://github.com/facebookresearch/demucs) | Optional local stem separation | MIT. Models and transitive packages may have separate terms and must be reviewed before redistribution. |
| [Basic Pitch](https://github.com/spotify/basic-pitch) | Optional local audio-to-MIDI transcription | Apache License 2.0. Models and transitive packages must be reviewed before redistribution. |
| [Python](https://docs.python.org/3/license.html) | Optional worker runtime | Python Software Foundation License. Python is not bundled. |

VoidNote includes no Warframe audio, art, model, executable or other Digital Extremes asset.
