# VoidNote Studio – Architekturstand Milestone E

## Geltungsbereich

Dieses Dokument beschreibt die implementierte Architektur nach **Milestone E – GameBridge**. Foundation, MIDI Core, Shawzin Codec, Composer/Arranger und die optionale, gekapselte Ingame-Wiedergabe sind enthalten. Multi-Shawzin, Audio Lab, Mandachord und Creator Mode bleiben späteren Milestones vorbehalten.

## Technische Basis

- .NET 10 LTS, zentral in `Directory.Build.props` und `global.json` festgelegt
- C# 14 mit aktivierten Nullable Reference Types und Warnungen als Fehler
- Avalonia 12.1 für die gemeinsame Windows-/Linux-Desktopanwendung
- Microsoft Extensions für Dependency Injection und Logging
- xUnit für Domain-, Modul- und Integrationstests
- DryWetMIDI 8.0.3 als stabile, MIT-lizenzierte SMF-Implementierung, ausschließlich in `VoidNote.Midi`
- zentrale Paketversionen in `Directory.Packages.props`

## Projekte und Verantwortlichkeiten

| Projekt | Verantwortung | Direkte Projektabhängigkeiten |
| --- | --- | --- |
| `VoidNote.Domain` | Normalisiertes Projekt- und Musikmodell, Master-Timeline, Tempo- und Taktarten-Map, Shawzin-Noten-/Chordmodell und Domain-Invarianten | keine |
| `VoidNote.Application` | Ports für Settings und Projektpersistenz, Undo/Redo sowie UI-unabhängige Piano-Roll-Projektion | Domain |
| `VoidNote.Infrastructure` | JSON-Settings, ZIP-basiertes `.vns`-Format, lokale JSON-Logs, Anwendungspfade | Application, Domain |
| `VoidNote.App` | Avalonia-Host, Composition Root, DI-Registrierung, MVVM-Shell | Application, Infrastructure |
| `VoidNote.Midi` | Gekapselter SMF-Import/-Export, Playback-Scheduler und Geräteverträge | Domain, DryWetMIDI |
| `VoidNote.Audio` | reservierte Modulgrenze; keine Audiofunktion implementiert | keine |
| `VoidNote.Shawzin` | Gekapselter Warframe-Shawzin-Songcode V1: Decoder, Encoder, Validierung, Fehlerdiagnostik und Codec-Fassade | Domain |
| `VoidNote.Mandachord` | reservierte Modulgrenze; keine Arrangementlogik implementiert | keine |
| `VoidNote.GameBridge` | Portable Input-Ports, Keybind-Profile, Mapping, Arm/Disarm, Diagnostik sowie getrennte Windows-/Linux-Adapter | Application, Domain, Shawzin |
| `VoidNote.PluginContracts` | reservierte Assembly-Grenze; kein öffentliches Plugin-System implementiert | keine |

## Abhängigkeitsrichtung

```text
VoidNote.App
    ├──> VoidNote.Application ───> VoidNote.Domain
    └──> VoidNote.Infrastructure ─> VoidNote.Application
                                └─> VoidNote.Domain

VoidNote.Midi ───────────────────> VoidNote.Domain
      └── intern: DryWetMIDI 8.0.3

VoidNote.Shawzin ────────────────> VoidNote.Domain
      └── keine externe Codec-Dependency

VoidNote.Domain ─────────────────> keine externen, UI- oder MIDI-Library-Abhängigkeiten
```

Öffentliche MIDI- und Shawzin-Schnittstellen verwenden ausschließlich BCL- und VoidNote-Typen. Weder Domain noch Application oder UI sehen DryWetMIDI-Typen; das Shawzin-Modul kennt weder DryWetMIDI noch Avalonia. Architekturtests prüfen diese Grenzen automatisch.

## Zentrales Projekt- und Zeitmodell

`VoidNoteProject` bleibt die versionierte Wurzel des normalisierten Modells (`formatVersion = 1`). `MidiTrack` enthält `MusicalEvent`-Noten mit stabiler ID, Starttick, Dauer, Pitch, Velocity, Herkunft und Confidence.

`ShawzinTrack` bleibt ein `ProjectTrack` und ergänzt dessen normalisierte Eventgrenze um die code-relevante Skala sowie geordnete physische `ShawzinEvent`-Anschläge. Diese enthalten stabile IDs, präzise absolute Positionen sowie `ShawzinNote`/`ShawzinChord` aus Saiten- und Fretwerten. Dadurch bleibt das Codeformat frei von MIDI-Pitches und kann dennoch über `ProjectTimeline` verlustarm auf die gemeinsame Timeline projiziert werden. Eine musikalische Pitch-Zuordnung hängt zusätzlich von Instrument und Tuning ab und wird bewusst erst im Arranger-Milestone implementiert.

`ProjectTimeline` speichert ganzzahlige Ticks mit projektweiter PPQ-Auflösung. Sie enthält geordnete `TempoChange`- und `TimeSignatureChange`-Maps und konvertiert zwischen:

- MIDI-Ticks und Viertelnoten-Beats
- MIDI-Ticks und nullbasierte, bei Bedarf gebrochene Taktkoordinaten
- MIDI-Ticks und `MusicalPosition` (Takt, Taktbeat, Tick im Beat)
- MIDI-Ticks und hochpräziser `AbsoluteTime` in Dezimalsekunden

Tempoänderungen werden segmentweise integriert. Millisekunden sind weder primäre Speicherung noch Konvertierungsbasis. Taktartenwechsel beginnen definitionsgemäß einen neuen Takt; dadurch bleiben auch nicht taktgrenzengenaue SMF-Wechsel eindeutig vorwärts abbildbar.

## MIDI-Modul

`IMidiFileImporter` und `IMidiFileExporter` bilden die stabilen Modulgrenzen. `DryWetMidiFileImporter` und `DryWetMidiFileExporter` sind die derzeitigen Adapter. Details zu Pipeline, Rundung und Einschränkungen stehen in `docs/midi.md`.

Der Import normalisiert PPQ, Tempo, Taktarten, Tracknamen und Noten. Der Standardexport erzeugt eine Format-1-Datei mit eigener Conductor-Spur und einer Spur je geeignetem VoidNote-MIDI-Track.

## Shawzin-Codec

`IShawzinCodeDecoder`, `IShawzinCodeEncoder` und `IShawzinCodeValidator` sind getrennt testbare Modulgrenzen; `IShawzinCodec` bietet eine optionale Fassade. Die konkreten `WarframeShawzinCode*`-Implementierungen verarbeiten ausschließlich die explizit dokumentierte Recorded-Song-V1-Variante.

Der Decoder liefert strukturierte Ergebnisse mit Fehlerkategorie, Codeposition, Symbol und Eventindex. Der Encoder ist deterministisch, validiert vor dem Schreiben und meldet jede notwendige 1/16-Sekunden-Quantisierung. Unvollständige Events, ungültige Zeichen, nicht klingende Spielsymbole, rückläufige oder gleiche Timestamps, nicht darstellbare Chords, Bereichsüberschreitungen und Quantisierungskollisionen werden abgelehnt. Die vollständige Wire-Spezifikation, Annahmen und Quellen stehen in `docs/shawzin-code-format.md`.

Der Codec bleibt von MIDI-Zuordnung, Skalenwahl, Compatibility-Bewertung und Wiedergabe getrennt. Diese Aufgaben liegen in eigenständigen Milestone-D-Services.

## Shawzin Composer und Arranger

`ShawzinDefinition` verbindet ein wiederverwendbares physisches `ShawzinPlayProfile` mit einem unabhängigen `ShawzinSoundProfile`. Skalen enthalten geordnete Pitch-/Input-Positionen; Algorithmen besitzen keine instrumentabhängigen Switch-Blöcke. Die eingebauten Dax- und Nelumbo-Definitionen teilen dasselbe Spielprofil, aber nicht ihr Klangprofil.

`IShawzinPitchMapper`, `IShawzinCompatibilityAnalyzer`, `IShawzinScaleAnalyzer`, `IShawzinTranspositionAnalyzer` und `IShawzinArranger` arbeiten ausschließlich auf Domainmodellen. Der Arranger erzeugt einen `ShawzinTrack` und einen vollständigen `ArrangementReport`. Die Application-Schicht koordiniert über `IShawzinStudioWorkflow` MIDI-Import, Analyse, Arrangement, Encoder und Preview; die Avalonia-View enthält nur Darstellung und plattformspezifische Datei-/Clipboard-Auswahl.

`ShawzinPlaybackEngine` plant alle Anschläge relativ zu einem einzigen monotonic-clock-Anker und gibt ausschließlich über `IShawzinPlaybackOutput` aus. Sie kennt weder Avalonia noch Betriebssystemeingaben. `SyntheticShawzinPreviewRenderer` erzeugt eigenständiges Mono-PCM-WAV aus synthetischen Pluck-Tönen und verwendet keine Warframe-Audiodateien. Ausführliche Regeln stehen in `docs/shawzin.md` und `docs/shawzin-arrangement.md`.

## Playback und Geräte

`MidiPlaybackEngine` erzeugt aus der gemeinsamen Timeline absolute Note-On-/Note-Off-Zielzeiten. `IPlaybackScheduler` arbeitet gegen einen festen monotonic-clock-Anker; Wartefehler werden daher nicht von Event zu Event aufsummiert. Der Transport bietet Play, Pause, Stop, Seek und Cancellation. `IMidiPlaybackOutput` hält die Ausgabe austauschbar.

`IMidiDeviceProvider`, `IMidiInputDevice` und `IMidiOutputDevice` definieren die Erweiterungsgrenze für spätere Plattformadapter und Recording. Milestone B enthält bewusst keine konkrete native Geräteimplementierung und keinen vollständigen Recorder.

## Piano Roll

`PianoRollViewModel` in Application ist eine schreibgeschützte, Avalonia-unabhängige Projektion eines normalisierten MIDI-Tracks. Sie stellt Ticks, Beats, musikalische und absolute Positionen für eine spätere Oberfläche bereit. Editierwerkzeuge, Quantisierung und aufwendige Darstellung sind noch nicht implementiert.

## GameBridge

`GameBridgePlaybackOutput` implementiert den bestehenden `IShawzinPlaybackOutput`-Port. Die Kette lautet `ShawzinPlaybackEngine → GameBridgePlaybackOutput → ShawzinInputMapper → IGameInputBridge`. Der Shawzin-Scheduler bleibt die einzige Zeitquelle und plant weiter gegen einen gemeinsamen monotonic-clock-Anker. Die Domain- und Shawzin-Assemblies referenzieren weder Win32 noch X11.

Die Bridge ist standardmäßig disarmed. Profilvalidierung, Fokusprüfung und Capability-Prüfung erfolgen vor realem Input; Stop, Fehler, Emergency Stop und Shutdown lösen `ReleaseAllAsync` aus und disarmen. Profile werden in einer atomar geschriebenen, versionierten lokalen JSON-Datei gespeichert. Details stehen in `docs/gamebridge.md`.

## Persistenz und Foundation-Dienste

Die Milestone-A-Architektur bleibt bestehen: `.vns` ist ein ZIP-Container mit `project.json`; Settings und Projekte werden atomar geschrieben; Logging bleibt lokal und ohne Telemetrie; Undo/Redo bleibt UI-unabhängig. Ältere Version-1-Projekte ohne Taktarten-Map erhalten beim Laden die Default-Taktart 4/4.

## Bewusst offene Punkte nach Milestone E

- MIDI-Kanäle, Program Changes, Controller, Marker und SysEx sind noch nicht Teil des normalisierten Domain-Modells.
- SMPTE-Time-Division wird abgelehnt; der MIDI Core verwendet PPQ.
- Leere SMF-/Conductor-Spuren werden nicht als Domain-Track angelegt; globale Tempo- und Taktartdaten werden separat übernommen.
- Seek reartikuliert keine bereits vor der Zielposition gestartete, noch klingende Note.
- Noch keine native MIDI-Geräteimplementierung und kein vollständiges Recording.
- Noch keine Piano-Roll-GUI, Editieroperationen oder Quantisierung.
- Migration, Autosave und Crash Recovery bleiben spätere Foundation-Erweiterungen.
- Digital Extremes veröffentlicht keine normative Shawzin-Wire-Spezifikation; UI-/Chat-/Versionsgrenzen für 100, 1000 oder 1666 Noten sind von der strukturellen 4096-Timestamp-Grenze getrennt dokumentiert.
- Slow Playback ist nicht im Songcode markiert und wird deshalb nicht als implizite zweite Codec-Zeitbasis behandelt.
- Das eingebaute Standard-Spielprofil ist eine dokumentierte, erweiterbare VoidNote-Projektion; die Community-/Spielvalidierung weiterer Tunings und Instrumentvarianten bleibt offen.
- Die minimale UI rendert eine synthetische WAV-Vorschau zum Speichern. Ein plattformübergreifender Live-Audio-Geräteadapter ist noch nicht enthalten; virtuelle Event-Wiedergabe und Preview-Rendering sind vollständig gekapselt.
- Ein systemweiter Emergency-Stop-Hotkey ist noch nicht implementiert; der jederzeit sichtbare UI-Emergency-Stop ist vorhanden.
- Wayland wird für reale Eingabesimulation bewusst als nicht verfügbar gemeldet.
- Multi-Shawzin-Aufteilung bleibt Milestone F.
