# MIDI Core

## Dependency und Kapselung

VoidNote MIDI verwendet `Melanchall.DryWetMidi` **8.0.3**. Diese Version ist die aktuelle stabile NuGet-Version; 9.x ist zum Implementierungszeitpunkt nur als Prerelease verfügbar. DryWetMIDI ist MIT-lizenziert und deckt robustes Lesen und Schreiben von Standard MIDI Files sowie die Note-Erkennung aus Note-On-/Note-Off-Paaren ab.

Die Dependency liegt ausschließlich in `VoidNote.Midi`. Öffentliche Modulgrenzen (`IMidiFileImporter`, `IMidiFileExporter`, Playback- und Geräteinterfaces) verwenden nur VoidNote- und .NET-Typen. Das Domain-Projekt referenziert weder DryWetMIDI noch Avalonia.

## Import-Pipeline

```text
.mid Stream
    ↓ cancellation-fähiges Puffern
DryWetMIDI SMF Parser
    ↓ PPQ + absolute MIDI-Events
Tempo-/Taktarten-Normalisierung
    ↓
ProjectTimeline
    ↓ Note-On-/Note-Off-Paarung je Spur
MidiTrack[] + MusicalEvent[]
```

Unterstützt werden Format-0- und Format-1-SMF-Dateien mit PPQ-Time-Division, mehrere musikalische Spuren, Tracknamen, Note On, Note Off (einschließlich der üblichen Note-On-Velocity-0-Darstellung), Pitch, Velocity, Dauer, Tempoänderungen und Taktartenwechsel. Jede importierte Note erhält `MusicalEventSource.ImportedMidi` und Confidence `1`.

Reine Conductor-/Metadatenspuren ohne Noten werden nicht als `MidiTrack` angelegt. Ihre Tempo- und Taktartdaten fließen trotzdem in die gemeinsame Timeline ein. Dadurch bleibt die musikalische Trackzuordnung beim Import-Export-Reimport stabil.

## Internes Timing-Modell

Die Primärdarstellung besteht aus nicht negativen, ganzzahligen `MusicalTime`-Ticks und einer projektweiten PPQ-Auflösung. Daraus berechnet `ProjectTimeline`:

- Viertelnoten-Beats als `decimal`
- nullbasierte, bei Bedarf gebrochene Taktkoordinaten als `decimal`
- `MusicalPosition` als einbasierter Takt und Beat plus nullbasierter Tick im Beat
- `AbsoluteTime` als `decimal`-Sekunden

Tempoänderungen werden stückweise integriert. Beispiel: Bei 120 BPM bis Tick 960 und danach 60 BPM werden die beiden Abschnitte mit ihren jeweiligen Sekunden-pro-Tick-Faktoren addiert. Es gibt keine Speicherung in gerundeten Millisekunden.

Taktartenwechsel beginnen in der musikalischen Positionsdarstellung einen neuen Takt. Liegt ein Wechsel mitten in einem Takt, ist der vorherige Takt entsprechend verkürzt. Die Tickposition selbst bleibt unverändert und verlustfrei.

## Tempo Map und Rundung

SMF speichert Tempo als ganzzahlige Mikrosekunden pro Viertelnote. Beim Import wird daraus exakt in `decimal` gerechnet:

```text
BPM = 60,000,000 / microsecondsPerQuarterNote
```

Beim Export muss eine beliebige Domain-BPM wieder auf den ganzzahligen SMF-Wert abgebildet werden. VoidNote rundet genau an dieser Formatgrenze auf die nächste ganze Mikrosekunde mit `MidpointRounding.AwayFromZero`. Tests begrenzen die resultierende BPM-Abweichung explizit. Tickpositionen, Notenstart und -dauer werden bei unveränderter PPQ ohne Rundung exportiert.

Die Rückkonvertierung von absoluter Zeit zu Ticks rundet ebenfalls erst am Ziel auf den nächsten Tick, mit `MidpointRounding.AwayFromZero`. Tick → absolute Zeit → Tick ist für aus Ticks erzeugte Zeitwerte auch über lange Strecken und Tempoänderungen stabil.

## Standardexport

Der Standardexport schreibt ein Format-1-SMF:

1. Conductor-Spur mit Tempo Map und Time Signatures
2. eine Spur je übergebenem `MidiTrack`
3. Trackname, Note On und Note Off mit Pitch, Starttick, Dauer und Velocity

Der Export arbeitet streambasiert, unterstützt Cancellation und übernimmt den Zielstream nicht. Library-spezifische Typen verlassen den Adapter nicht.

## Roundtrip-Verhalten

Die Testpipeline lautet:

```text
programmatisch erzeugtes, library-unabhängiges SMF Fixture
    ↓ Import
VoidNote Timeline + Tracks
    ↓ Export
Format-1 SMF
    ↓ Reimport
VoidNote Timeline + Tracks
```

Verglichen werden Trackanzahl und -namen, Notenanzahl, Pitch, Velocity, Starttick, Dauer, PPQ, Tempoänderungen und Taktartenwechsel. Für Ticks gilt exakte Gleichheit. Für nicht direkt als ganze SMF-Mikrosekunden darstellbare BPM gilt die oben definierte Rundung.

Die Fixtures decken einzelne und aufeinanderfolgende Noten, Akkorde, mehrere Tracks, verschiedene Velocities, einen Tempowechsel, verschiedene Taktarten und langes Timing ohne Drift ab. Sie werden vollständig lokal erzeugt und benötigen kein Netzwerk.

## Playback Core

`MidiPlaybackEngine` projiziert Noten über `ProjectTimeline` auf absolute Note-On-/Note-Off-Zeitpunkte. Alle Ziele werden relativ zu demselben monotonic-clock-Anker an `IPlaybackScheduler` übergeben. Es wird nicht jeweils ab dem vorherigen Event geschlafen; eine verspätete Ausgabe verschiebt daher nicht automatisch den gesamten Rest des Songs.

Der Transport unterstützt Play, Pause, Stop, Seek und Cancellation. Pause, Stop, Seek und Disposal senden über `IMidiPlaybackOutput.AllNotesOffAsync` eine Bereinigungsanforderung. Eine konkrete Synthesizer- oder Hardwareausgabe ist nicht Teil dieses Milestones.

## Bekannte Einschränkungen

- Nur PPQ-Time-Division; SMPTE wird mit `MidiFileException` abgelehnt.
- MIDI-Kanal, Program Changes, Controller, Marker, Aftertouch und SysEx werden noch nicht normalisiert oder roundtrip-erhalten.
- Leere musikalische Spuren werden beim Import nicht als Domain-Tracks angelegt.
- Seek startet Noten nicht nachträglich, wenn deren Note On vor und deren Note Off nach der Zielposition liegt.
- Der Standardexport nutzt MIDI-Kanal 1 (nullbasierter Kanal 0).
- Geräteinterfaces sind vorbereitet, konkrete OS-/Backend-Adapter und vollständiges Recording folgen später.
