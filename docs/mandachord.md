# Mandachord Studio

## Umfang und Modulgrenze

Milestone J implementiert Mandachord als eigenes, UI-unabhängiges Domain-/Service-Modul. Es liest ausschließlich normalisierte `MusicalEvent`-Daten oder bereits vorhandene Rhythmus-/Onset-Metadaten. Es enthält keine Audio-to-MIDI-Engine, keine Shawzin-Codec-Kopie, keine GameBridge und keine Betriebssystemeingabe.

## Rastermodell

`MandachordGridDefinition.Standard` ist die einzige Datenquelle für Rasterparameter:

- 4 Takte in 4/4;
- 4 Sechzehntel-Steps je Viertelnote;
- 16 Steps je Takt, 64 Steps je Loop;
- fester Ingame-Bezug 120 BPM, damit 0,125 Sekunden je Step und 8 Sekunden je Loop;
- fünf tonale Positionen D, F, G, A, C für Bass und Melody;
- drei Percussion-Slots: Kick, Snare und Hi-Hat.

Community-Referenzen: [WARFRAME Wiki – Mandachord](https://warframe.fandom.com/wiki/Mandachord) dokumentiert 120 BPM, Sechzehntel-Slots und D-Moll-Pentatonik. Die ursprünglichen Update-20-Patchnotes werden in der [Community-Kopie zu Octavia's Anthem](https://www.reddit.com/r/Warframe/comments/61b6i4/octavias_anthem_update_20/) mit vier Takten und den drei Instrumentsektionen wiedergegeben. Das Open-Source-Projekt [mandascore](https://github.com/buff0000n/mandascore) dient nur als sekundäre Gegenprüfung der 64×13-Gridaufteilung. Keine dieser Quellen wird als offizielle Wire-Spezifikation behandelt.

Annahme: Die Quellen normieren die fünf Pitchklassen, aber keine verlässliche MIDI-Oktavlage. VoidNote verwendet daher D3/F3/G3/A3/C4 für Bass und D4/F4/G4/A4/C5 für synthetische Preview und MIDI-Repräsentation. Diese Oktavwerte behaupten keine native Warframe-Interchange-Semantik.

## Domainmodell

`MandachordArrangement` hält Patterns, Sections, gewähltes SoundSet und Preset. `MandachordPattern` ist ein Metadatenobjekt der Projektbibliothek; `MandachordStep` hält Layer, Step, Pitchposition oder Percussionkategorie, Velocity und Provenienz. Sections ordnen längere Songs als Intro/Verse/Chorus usw. auf unterschiedliche Loop-Patterns der gemeinsamen Master Timeline ab.

## Pitch und Timing

Das Pitch-Mapping unterscheidet exakt, oktavverschoben, durch kleine Transposition besser und nicht sinnvoll. Jede tatsächliche Änderung landet im Generation Report. Timing wird je Event unabhängig als `round((sourceBeat-loopStartBeat)×4, AwayFromZero)` berechnet und modulo 64 projiziert. Dadurch entsteht keine kumulative Drift. Quantisierungsfehler und Mehrfachbelegung desselben Layer-Steps werden explizit gemeldet.

## Quellen und Provenienz

Unterstützt sind MIDI-, VoidNote-, Audio-Transkriptions-, stem-abgeleitete MIDI- und normalisierte Shawzin-Tracks. Eine Shawzin-Quelle benötigt normalisierte Pitches; physische Songcode-Eingaben allein sind nicht eindeutig. `AudioRegion` ist nur zusammen mit einer vorhandenen Analyse-ID und Analyseevents zulässig. Automatische Steps behalten Source Track, Source Event, Generatorversion und Preset. Manuelle Änderungen ergänzen die Provenienzliste, statt sie zu ersetzen.

## Editor, Preview und SoundSets

Der Editor unterstützt Set/Delete, Pitch-/Percussionänderung, Mehrfach-ID-Auswahl, Copy/Paste, Clear, Candidate-Übernahme, Patternlöschung, Section Assignment und SoundSet-Wechsel über Undo/Redo. Pattern und `MandachordSoundSet` sind getrennt. Das Default-Set erzeugt eigene Sinus-/Noise-Sounds ohne Warframe-Dateien. Ein offline PCM-Mixer kann kompatible synthetische Shawzin- und Mandachord-WAVs kombinieren; sample-genauer Hardware-Livemix ist nicht garantiert.

## Export und Persistenz

Projektformat 4 persistiert Arrangements, Patternbibliothek, Sections, SoundSets und Provenienz im `.vns`-Manifest. v1/v2/v3 werden in-memory ergänzt; vor dem ersten Überschreiben entsteht `.v1.bak`, `.v2.bak` oder `.v3.bak`. JSON-Export heißt ausdrücklich `VoidNote Mandachord`. MIDI enthält getrennte Percussion-, Bass- und Melody-Spuren und ist ebenfalls nur eine VoidNote-Repräsentation. Es existiert kein behaupteter nativer Warframe-Share-Codecodec.

## Grenzen

- Gameplay Score ist eine transparente Heuristik, keine objektive Optimalitätsbehauptung.
- Lange Songs werden über mehrere manuell/automatisch zugeordnete Loop-Sections repräsentiert; Warframe selbst spielt weiterhin jeweils ein Loop-Pattern.
- Keine Mandachord-Eingabesimulation, Octavia-Automation oder Warframe-Prozessinspektion.
