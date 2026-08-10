# Multi-Shawzin Splitter und Ensemble Arranger

## Datenfluss und Grenzen

```text
MidiTrack[] / normalisierte MusicalEvent[]
    → IMultiShawzinSplitter
    → SplitVoice[] + MultiShawzinSplitReport
    → ShawzinEnsembleArranger
    → Analyse + Arrangement je Stimme
    → ShawzinEnsemble
    → Preview / unabhängige Codes / ausgewählter Track zur GameBridge
```

Splitter und Ensemble kennen nur VoidNote-Domainmodelle und Shawzin-Services. Es existieren keine DryWetMIDI-, Avalonia-, Audio-, Mandachord-, GameBridge- oder OS-Input-Abhängigkeiten. Alle Tracks referenzieren dieselbe `ProjectTimeline`; es werden keine unabhängigen Zeitbasen erzeugt.

## Voice Separation

Noten werden stabil nach Starttick, Pitch und ID sortiert und pro Startgruppe verarbeitet. Jede mögliche Zielstimme erhält einen Score aus Rollenpassung, lokaler Sprungweite, zeitlichem Anschluss, Registerpassung und einer weichen Lastkomponente. Überlappung sowie die erneute Verwendung derselben Stimme innerhalb einer Startgruppe werden bestraft. Solange freie Stimmen vorhanden sind, werden gleichzeitig beginnende Noten auf verschiedene Tracks verteilt. Gleichstände werden immer über den Trackindex gebrochen.

Die Kontinuität einer Stimme ist `1 - min(1, abs(PitchDelta) / 24)`. Der zeitliche Anschluss vergleicht den neuen Start mit dem Ende der letzten Note. Eine Überlappung liegt vor, wenn `Last.Start + Last.Duration > Current.Start`. Dauer und Sustain beeinflussen Melody-/Bass-Salienz und Überlappung; Velocity und lokale Dichte beeinflussen die Melody-Salienz.

## Melody Detection

Der Melody Score ist auf `0..1` begrenzt:

```text
0,34 × relative Tonhöhe in der lokalen Startgruppe
+ 0,22 × Velocity / 127
+ 0,16 × min(1, Dauer / 1920 Ticks)
+ 0,20 × Kontinuität zur vorherigen Melodienote
+ 0,08 × 1 / lokale Notenzahl
```

Dadurch gewinnt nicht automatisch immer nur die höchste Note: ein extrem springender, kurzer und leiser Spitzenton kann hinter einer stabilen Hauptlinie liegen.

## Bass Detection

Der Bass Score ist ebenfalls `0..1`:

```text
0,42 × tiefes relatives Register
+ 0,22 × min(1, Dauer / 1920 Ticks)
+ 0,22 × Kontinuität zur vorherigen Bassnote (18-Halbton-Fenster)
+ 0,14 × rhythmischer Anschluss an deren Ende
```

Damit ist die Bassstimme nicht schlicht die jeweils tiefste Note; eine stabile, längere Linie kann einen kurzfristigen tiefen Ausreißer überstimmen.

## Split Strategies

- `MelodyHarmony`: Track 1 verstärkt Melody-Salienz; weitere Stimmen werden durch Kontinuität, Register und Balance verteilt.
- `MelodyBass`: Track 1 verstärkt Lead-, Track 2 Bass-Salienz; weitere Tracks nehmen Reststimmen auf.
- `RegisterSplit`: Zielregister sind gleichmäßig von oben nach unten angeordnet.
- `FullEnsemble`: kombiniert rollen-neutral Kontinuität, Register, Überlappung und Balance.
- `MinimalNoteLoss`: priorisiert freie Kapazität und geringe Überlappung. Der Splitter selbst verwirft dabei keine Note.
- `MaximumRecognition`: verstärkt charakteristische Melody-Salienz auf Track 1.
- `CreatorMultitrack`: verstärkt zeitlich und melodisch eigenständige Stimmen für getrennte Takes.

Alle Strategien sind deterministisch. Standardnamen sind `Lead`, `Harmony 1`, `Harmony 2`, `Bass`, `Counter Melody`, `Upper Register`, `Middle Register`, `Lower Register` und bei höheren Zahlen fortlaufende Harmony-Namen. Namen bleiben editierbar.

## Balance und Ensemble Optimization

Die Trackverteilung ist `TrackAssigned / Assigned × 100`. Der Balance Score vergleicht jede Verteilung mit `100 / TrackCount` und zieht die mittlere absolute Abweichung ab. Dieser Wert ist Diagnose, kein hartes Gleichverteilungsziel: Rollenpassung und Kontinuität besitzen im Zuordnungsscore Vorrang.

`EnsembleOptimizationReport` aggregiert Quell-, arrangierte, verworfene und doppelte Noten, Note Loss, durchschnittliche und niedrigste Compatibility, Voice Continuity, Balance sowie die mittlere absolute Pitchänderung aus Transposition, Oktavshift und Pitchsubstitution. Empfehlungen werden nur aus transparenten Schwellenwerten erzeugt.

## Split Report und Datenintegrität

Jede `SplitAssignment` enthält Quelltrack, Quellnote, Pitch, Zeit, Zieltrack, Strategie, Score, Confidence und einen ausformulierten Grund mit Rollen-, Kontinuitäts-, Zeit-, Balance- und Überlappungsanteilen. Drop- und Duplicate-Kennzeichen sind explizit. Es gibt keinen stillen Note Loss: `Source = Assigned + Dropped`, wobei Duplikate separat gezählt werden. Spätere Ton-, Timing- und Drop-Änderungen bleiben im `ArrangementReport` der jeweiligen Ensemble-Spur erhalten.

## Per-Track-Optimierung

Jede Stimme erhält über die bestehenden Services Scale Candidates, Transposition Suggestions, Compatibility und Arrangement inklusive 1/16-Sekunden-Quantisierung und Chordbehandlung. Instrument, Skala, Transposition und Arrangement-Flags dürfen je Stimme verschieden sein. Der Shawzin Codec bleibt unverändert.

## Manual Reassignment

`IEnsembleReassignmentService.MoveNotes` verschiebt eine oder mehrere stabile Note-IDs von Track A nach Track B. Ein `IUndoableCommand` bewahrt dieselben normalisierten Events, sortiert beide Tracks reproduzierbar und berechnet nur Quelle und Ziel erneut. Derselbe Command unterstützt Undo und Redo; Compatibility, Arrangement, Ensemble-Metriken, Preview und Export können danach aktualisiert werden.

## Playback und Preview

`ShawzinEnsemblePlaybackEngine` führt die hörbaren Events aller Tracks in eine geordnete Queue und plant jedes Ziel gegen denselben monotonic-clock-Anker. Play, Pause, Stop, Seek, einzelner Track, Active, Mute und Solo werden unterstützt. Mute/Solo werden unmittelbar vor Dispatch erneut geprüft.

`SyntheticShawzinEnsemblePreviewRenderer` rendert 16-Bit-Stereo-WAV. Tracks erhalten eine leichte Panorama-Verteilung; Klangprofile dürfen eine kleine synthetische Oberton-/Hüllkurvenvariation wählen. Es werden keine Warframe-Samples verwendet.

## Multi-Code Export

`EnsembleCodeExporter` kodiert jeden erfolgreichen `ShawzinTrack` unabhängig. Der Bericht enthält Trackname, Instrument, Skala, Transposition, Eventzahl, Dauer, Compatibility, Code, Codelänge, Validierungsstatus und strukturierte Fehler. Eine fehlgeschlagene Spur verhindert nicht, dass erfolgreiche Spuren samt eigenem Status sichtbar bleiben.

## Übergabe an Creator Mode

`CreatorSessionFactory` übernimmt ausgewählte Ensemble-Spuren, Namen, Instrument, Skala, Transposition, Arrangementstrategie, Eventzahl und die bereits über den bestehenden Codec erzeugten individuellen Codes. Der Wizard darf Spuren ein- oder ausschließen. Audio-Transkriptionsherkunft aus den normalisierten Source Events wird als Provenienztext weitergereicht. Alle Takes referenzieren dieselbe Projekt-Timeline; `CreatorTimingService` erzeugt für jeden Take denselben absoluten Music Start. Retakes kopieren die Quellenzuordnung in einen neuen historisch erhaltenen Versuch.
