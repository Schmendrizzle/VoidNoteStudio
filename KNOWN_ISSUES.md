# Known Issues — 1.0.0-rc1

- Linux real GameBridge input supports X11/XTest only. Wayland is intentionally unsupported; composition, preview and export remain available.
- FFplay targets the system default output device; native device enumeration/selection is not implemented.
- FFmpeg, Demucs, Basic Pitch and Python are optional manual installations and are not bundled.
- Audio/stem and synthetic preview clocks avoid cumulative scheduler drift but are not a sample-accurate DAW hardware clock.
- Shawzin play profiles are data-driven but still require broader manual validation against current in-game instruments. The validation-record tool is provided for this work.
- Digital Extremes publishes no normative Shawzin wire-format specification. Game/version/UI transfer limits can differ from the structural codec limit.
- MIDI channel/program/controller/marker/aftertouch/SysEx roundtrip and SMPTE time division are not supported by the normalized RC model.
- There is no native MIDI-device backend, complete live MIDI recorder or full graphical Piano Roll editor in this RC.
- The compact Mandachord view is an inspector/editor over the complete model, not a native Warframe share-code exporter.
- AppImage and a Windows installer are not shipped in rc1; portable framework-dependent ZIP/tar.gz packages require .NET 10.
- Some operation-specific progress/error sentences remain English even when German static UI resources are selected; core navigation, actions and settings are localized. This must be completed before a final 1.0 label.
