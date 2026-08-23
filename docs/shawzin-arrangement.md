# Shawzin Arrangement

## Analyse vor der Konvertierung

`IShawzinCompatibilityAnalyzer` zählt Noten, direkt spielbare, nicht verfügbare, außerhalb des Bereichs liegende und oktavweise reparierbare Noten. Gleichzeitig startende Noten werden als Gruppe untersucht. Mehr als drei Stimmen ergeben einen Polyphoniekonflikt; bis zu drei Noten gelten nur dann als gültiger Chord, wenn eine Kandidatenkombination unterschiedliche Saiten unter derselben Fretmaske besitzt.

Startzeiten werden für die Analyse direkt von der `ProjectTimeline` in Dezimalsekunden projiziert und auf `0,0625 s` geprüft. Verschiedene Quellzeitpunkte, die denselben Zielwert erhalten, sind Quantisierungskollisionen. Ein gleitendes Ein-Sekunden-Fenster markiert Dichte oberhalb von zwölf Noten.

## Compatibility Score (technische Playability)

Jede Quellnote erhält zuerst einen Pitch-Qualitätswert `q` und eine erwartete Distanz `d` in Halbtönen:

```text
Direct:       q = 1.00, d = 0
Octave:       q = max(0.40, 0.70 - 0.15 × (Octaves - 1))
Substitution: q = max(0.00, 0.30 - 0.05 × d)
Drop:         q = 0.00

PitchScore = 100 × mean(q)
ChangeRate = 100 × (Octave + Substitution + Drop) / TotalNotes

Penalty = 1.5 × MeanPitchError
        + 0.15 × ChangeRate
        + min(12, 0.20 × 100 × TimingConflicts / TotalNotes)
        + min(12, 0.25 × 100 × PolyphonyConflicts / TotalNotes)
        + min(12, 0.20 × 100 × ChordConflicts / TotalNotes)
        + min( 6, 0.10 × 100 × DenseWindows / TotalNotes)

OverallPlayability = round(clamp(PitchScore - Penalty, 0, 100))
```

Das Ergebnis wird mit `MidpointRounding.AwayFromZero` auf eine ganze Zahl gerundet. Eine leere Spur erhält 100, weil keine inkompatiblen Inhalte vorliegen. Einzelmetriken bleiben sichtbar, sodass die Zahl erklärbar ist.

## Scale Ranking

`IShawzinScaleAnalyzer` bewertet die realen absoluten Pitchmengen. Pro Skala werden `DirectlyPlayable`, `OctaveFixable`, `NotPlayable`, notwendige `PitchSubstitutions`, mittlere und maximale Pitchabweichung ausgegeben. Pitch-Class-Fit bleibt eine Diagnose, dominiert aber nicht das Ranking.

```text
Suitability = clamp(100 × mean(q)
              - 1.5 × MeanPitchError
              - 0.25 × DropRate, 0, 100)
```

Es gibt keinen Skalenbonus. Eine kleinere Skala kann Chromatic nur durch mindestens gleich gute reale Tonhöhenabdeckung schlagen; viele zugewiesene, aber veränderte Noten erzeugen keinen hohen Rang.

## Transpositionsvorschläge

`IShawzinTranspositionAnalyzer` prüft standardmäßig jeden Halbtonschritt von `-12` bis `+12`, ohne ihn anzuwenden. Jede Zeile enthält direkte Treffer, Oktavkorrekturen, Substitutionen, Drops, Konflikte sowie mittlere und maximale Abweichung vom Originalpitch. Die Pitchqualität folgt der obigen Gewichtung; zusätzlich kostet jeder angewandte Transpositionshalbton `0,025` Qualitätsanteile pro Note. Der Score zieht `1,5 × MeanPitchError` und eine normalisierte Konfliktstrafe ab. Gleichstände bevorzugen die kleinere absolute Transposition und danach den negativen Wert.

## Arrangement-Strategien

- `Strict`: akzeptiert nur exakt darstellbare Pitches, Chords und bereits rastergenaue Zeiten. Transposition, Oktavwechsel, Pitchersatz, Timingverschiebung und stilles Löschen sind verboten. Jeder nicht darstellbare Fall wird `ConflictUnresolved`; es entsteht kein exportierbarer Track.
- `ClosestPitch`: verwendet den nächstgelegenen Pitch, bei Gleichstand den tieferen.
- `PreserveMelody`: priorisiert hohe Pitches, dann Velocity, und erhält die größte gültige Teilmenge.
- `OctaveShift`: wählt die nächstgelegene oktavgleiche Position.
- `DropLowest`: prüft hohe Stimmen zuerst und entfernt bevorzugt tiefe.
- `DropHighest`: prüft tiefe Stimmen zuerst und entfernt bevorzugt hohe.
- `Arpeggiate`: verteilt einen inkompatiblen Gleichzeitigkeitspunkt in 1/16-Sekunden-Schritten, standardmäßig höchstens über `0,1875 s`.
- `Simplify`: behält höchstens zwölf Noten in jedem gleitenden Ein-Sekunden-Fenster.

Strategien sind kombinierbare Flags. Sobald `Strict` enthalten ist, verbietet es eine konfigurierte Transposition. Jede Modifikation und jeder ungelöste Konflikt wird protokolliert. Der Studio-Workflow kodiert nur dann, wenn ein vollständiger Track ohne Strict-Konflikte existiert.

## Timing

Die hochpräzise VoidNote-Zeit bleibt bis zur Formatgrenze erhalten. Jeder Start wird unabhängig aus der Tempo Map in absolute Zeit umgerechnet und anschließend einmal mit `MidpointRounding.AwayFromZero` auf das Recorded-Song-V1-Raster quantisiert. Es gibt keine fortlaufende Addition gerundeter Deltas und daher keine kumulative Drift.

Kollisionen werden nie zusammengeführt oder verworfen. `Arpeggiate` darf sie innerhalb seiner Grenze nach hinten verschieben; andernfalls entsteht `ConflictUnresolved`. Der Report enthält maximalen und mittleren absoluten Quantisierungsfehler sowie die Anzahl kollidierter Events.

## ArrangementReport und eindeutige Change Rate

Jeder Eintrag enthält Quell-ID, Quell- und Zielpitch, ursprünglichen und neuen Tick, Änderungstyp, Begründung und Strategie. Der Report weist `SourceNoteCount`, `ExactNoteCount`, `OctaveShiftCount`, `PitchSubstitutionCount`, `DroppedNoteCount`, `ArpeggiatedCount`, `TimingModifiedCount`, `TotalChangedSourceNotes`, `ChangeRatePercent`, mittleren/maximalen Pitchfehler sowie mittleren/maximalen Timingfehler aus.

Die getrennten Typzähler dürfen dieselbe Note mehrfach enthalten, etwa wenn sie zuerst oktaviert und danach quantisiert wurde. `TotalChangedSourceNotes` bildet dagegen die Vereinigungsmenge der Quell-IDs. Deshalb kann eine alte Rohzählung wie `273 Notes / 280 Changes` durch mehrere Änderungen derselben Note entstehen; die neue zusammenfassende Change Rate überschreitet dadurch nicht 100 %. Ungelöste Strict-Konflikte gelten nicht als ausgeführte Änderungen und blockieren den Track.

## Musical Similarity Score

Playability beantwortet „Kann Warframe die Ausgabe technisch spielen?“. Musical Similarity beantwortet getrennt „Wie ähnlich bleibt sie der Quelle?“:

```text
OverallSimilarity = 0.35 × PitchPreservation
                  + 0.20 × MelodicContourPreservation
                  + 0.20 × NoteRetention
                  + 0.15 × TimingPreservation
                  + 0.10 × IntervalPreservation
```

- Exakte Pitches erhalten 100; eine einfache Oktavverschiebung 70 und weitere Oktaven je 15 weniger, mindestens 40. Nicht-oktavische Abweichungen fallen linear innerhalb von zwölf Halbtönen.
- Kontur vergleicht das Vorzeichen aufeinanderfolgender Intervalle.
- Retention ist der Anteil erhaltener Quellnoten.
- Timing fällt innerhalb eines 0,25-Sekunden-Fensters linear von 100 auf 0.
- Intervallerhalt fällt mit der Differenz zwischen Quell- und Zielintervall innerhalb von zwölf Halbtönen.

Monophone Quellen durchlaufen keine Voice-Reduction-Heuristik. Reihenfolge und Source-ID-Zuordnung bleiben erhalten; nur ausdrücklich gewählte Pitch-/Timingstrategien dürfen Änderungen erzeugen.

Ausgabe-IDs werden stabil aus Track-ID, Quell-IDs, Zieltimestamp und Diskriminator abgeleitet. Gleicher Input plus gleiche Optionen erzeugt damit dasselbe Arrangement und denselben Songcode.

## Multi-Shawzin-Anordnung

Vor dem bestehenden Einzeltrack-Arranger kann `IMultiShawzinSplitter` polyphone normalisierte Musik in zwei, drei, vier oder eine höhere benutzerdefinierte Stimmenzahl zerlegen. Die Split-Stufe verändert keine Pitches und quantisiert kein Timing. Danach erhält jede Stimme ihre eigene Skalenanalyse, Transpositionsrangliste, Compatibility-Bewertung und einen normalen `IShawzinArranger`-Durchlauf. Arrangement-Änderungen bleiben in den bereits definierten `ArrangementReport`-Einträgen nachvollziehbar. Pro Ensemble-Track werden Playability und Musical Similarity getrennt ausgewiesen. Der Gesamtbericht enthält Overall Playability, Overall Musical Similarity, Note Loss, Pitch Change Rate und Timing Change Rate.

Die bisherige `PreserveMelody`-Reduktion im Einzeltrack-Arranger bleibt kompatibel. Die genauere Melody-Heuristik des Splitters bewertet relative Höhe (34 %), Velocity (22 %), Dauer (16 %), Sprung-/Zeitkontinuität (20 %) und lokale Dichte (8 %). Bass-Salienz verwendet tiefes Register (42 %), Dauer (22 %), melodische Kontinuität (22 %) und rhythmische Stabilität (14 %). Details stehen in `docs/multi-shawzin.md`.

## Bekannte Einschränkungen

- Notendauern und Velocity beeinflussen den Recorded-Song-V1-Code nicht; sie bleiben nur im normalisierten Ausgabetrack erhalten.
- Melody- und Bass-Erkennung bleiben deterministische Heuristiken und keine AI- oder Partitursemantik.
- Arpeggiation verteilt nur vorwärts und erzeugt keine rhythmische Neuinterpretation.
- Manuelle Reassignment-Datenoperationen sind vorhanden; eine grafische Piano-Roll-Zuordnung bleibt späterer UI-Arbeit vorbehalten.
