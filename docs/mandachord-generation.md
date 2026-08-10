# Mandachord Generation und Scoring

## Deterministische Pipeline

```text
normalisierte Source Tracks + optionale Rhythmusmetadaten
→ unabhängige Step-Quantisierung
→ Melody-/Bass-/Percussion-Reduction
→ Patternkandidat
→ erklärbare Scores + vollständiger Änderungsreport
```

Sortierungen enden immer mit stabiler Source-ID; Kandidat-, Arrangement-, Pattern-, Section- und Step-IDs werden aus Input, Settings und Preset deterministisch abgeleitet. Gleicher Input erzeugt gleiche musikalische Daten und IDs.

## Presets

- `Faithful`: höchste Gewichtung von Repräsentations-, Pitch- und Timingtreue.
- `Recognizable`: verstärkt wiederholte Hook-Pitches und Melody Preservation.
- `Gameplay`: begrenzt Layerdichte stärker und bewertet Klarheit/Wiederholung; Abweichungen bleiben im Report.
- `RhythmFocus`: erhöht Percussionkapazität und reduziert tonale Dichte.
- `MelodyFocus`: erhöht Melodykapazität und deren Kontinuitätsgewicht.

Der Generator liefert standardmäßig drei Varianten: zuerst das angeforderte Preset, danach stabil geordnete Alternativen. Das Ranking verwendet den zum angeforderten Ziel passenden transparenten Score.

## Melody Reduction

Importance je Note ist die gewichtete Summe aus relativem Register (20 %), Dauer (18 %), Kontinuität zum Vorgänger (22 %), Pitchwiederholung (20 %) und Velocity (20 %). `MelodyFocus` ergänzt 15 Prozentpunkte, `Recognizable` bei deutlicher Wiederholung 12. Pro Layer/Step bleibt die höchstbewertete darstellbare Note. Pitchdistanz, Kontur/Continuity, Phraseanschluss, Repetition und Collision Avoidance sind damit explizit enthalten.

## Bass Reduction

Bass verwendet eine eigene Gewichtung: tiefes Register 28 %, Dauer 24 %, Kontinuität 20 %, Wiederholung 18 %, Velocity 10 %. Eine längere, wiederholte Root-/Basslinie kann deshalb einen einzelnen tieferen Ausreißer schlagen. `Gameplay` reduziert Bassdichte auf 65 % des konfigurierten Layerlimits.

## Percussion

Vorhandene `MandachordRhythmEvent`-Metadaten haben Vorrang. Ohne Drum-Analyse werden ausschließlich normalisierte Onsets rhythmisch kategorisiert: starke Takt-/Halbtaktpositionen Kick, Viertel 2/4 Snare, übrige Onsets Hi-Hat. Das erzeugt Mandachord-Kategorien, keine erfundenen Pitch-MIDI-Drums. Erst beim optionalen MIDI-Export werden die dokumentierten General-MIDI-Repräsentationsnoten 36/38/42 verwendet.

## Scores

Alle Komponenten werden auf 0–100 begrenzt und `AwayFromZero` auf zwei Dezimalstellen gerundet.

```text
Represented = tonal output / source notes
PitchAccuracy = 1 - pitch changes / tonal output
TimingAccuracy = 1 - timing changes / tonal output
Similarity = 45% Represented + 35% PitchAccuracy + 20% TimingAccuracy

MelodyPreservation = 55% normalized melody share + 45% PitchAccuracy
BassPreservation = 60% normalized bass share + 40% PitchAccuracy
RhythmMatch = 65% normalized percussion coverage + 35% TimingAccuracy
Density = closeness of total occupancy to documented heuristic target 22%
Gameplay = 35% collision clarity + 30% Density + 20% repetition + 15% RhythmMatch
```

Die 22-%-Dichte ist ein dokumentierter VoidNote-Heuristikzielwert für übersichtliche, wiederholbare Patterns, kein Warframe-Optimum. Source-/Outputzahlen, erhaltene, verschobene und verworfene Noten, Pitch-/Timingänderungen und Collision-Entscheidungen bleiben neben den Scores sichtbar.
