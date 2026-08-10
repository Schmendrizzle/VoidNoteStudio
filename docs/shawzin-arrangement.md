# Shawzin Arrangement

## Analyse vor der Konvertierung

`IShawzinCompatibilityAnalyzer` zählt Noten, direkt spielbare, nicht verfügbare, außerhalb des Bereichs liegende und oktavweise reparierbare Noten. Gleichzeitig startende Noten werden als Gruppe untersucht. Mehr als drei Stimmen ergeben einen Polyphoniekonflikt; bis zu drei Noten gelten nur dann als gültiger Chord, wenn eine Kandidatenkombination unterschiedliche Saiten unter derselben Fretmaske besitzt.

Startzeiten werden für die Analyse direkt von der `ProjectTimeline` in Dezimalsekunden projiziert und auf `0,0625 s` geprüft. Verschiedene Quellzeitpunkte, die denselben Zielwert erhalten, sind Quantisierungskollisionen. Ein gleitendes Ein-Sekunden-Fenster markiert Dichte oberhalb von zwölf Noten.

## Compatibility Score

Die Berechnung ist reproduzierbar:

```text
PitchScore = 100 × (Direct + 0.75 × OctaveFixable) / TotalNotes

Overall = clamp(PitchScore
  - min(10, 10 × TimingConflicts / StartGroups)
  - min(10, 10 × PolyphonyConflicts / StartGroups)
  - min(10, 10 × ChordConflicts / StartGroups)
  - min( 5,  5 × DenseWindows / StartGroups), 0, 100)
```

Das Ergebnis wird mit `MidpointRounding.AwayFromZero` auf eine ganze Zahl gerundet. Eine leere Spur erhält 100, weil keine inkompatiblen Inhalte vorliegen. Einzelmetriken bleiben sichtbar, sodass die Zahl erklärbar ist.

## Scale Ranking

`IShawzinScaleAnalyzer` ermittelt die verwendeten Pitch Classes. Pro unterstützter Skala werden direkte Pitchabdeckung und Pitch-Class-Abdeckung berechnet:

```text
Suitability = 0.70 × DirectCoverage + 0.30 × PitchClassFit + 5 (nicht chromatisch)
```

Der kleine, auf 100 begrenzte Bonus verhindert, dass Chromatic bei musikalisch gleich guter Abdeckung automatisch jede engere Skala verdrängt. Sortiert wird danach nach Score, direkter Abdeckung und stabiler Skalen-ID.

## Transpositionsvorschläge

`IShawzinTranspositionAnalyzer` prüft standardmäßig jeden Ganztonschritt von `-12` bis `+12`, ohne ihn anzuwenden. Direkt spielbare Noten zählen voll, oktavweise reparierbare zu 75 Prozent; verlorene Pitches zählen nicht. Polyphoniegruppen über drei Stimmen ziehen je fünf Punkte ab. Gleichstände bevorzugen die kleinere absolute Transposition und danach den negativen Wert.

## Arrangement-Strategien

- `Strict`: akzeptiert nur exakt darstellbare Pitches und Chords; Konflikte machen das Ergebnis erfolglos.
- `ClosestPitch`: verwendet den nächstgelegenen Pitch, bei Gleichstand den tieferen.
- `PreserveMelody`: priorisiert hohe Pitches, dann Velocity, und erhält die größte gültige Teilmenge.
- `OctaveShift`: wählt die nächstgelegene oktavgleiche Position.
- `DropLowest`: prüft hohe Stimmen zuerst und entfernt bevorzugt tiefe.
- `DropHighest`: prüft tiefe Stimmen zuerst und entfernt bevorzugt hohe.
- `Arpeggiate`: verteilt einen inkompatiblen Gleichzeitigkeitspunkt in 1/16-Sekunden-Schritten, standardmäßig höchstens über `0,1875 s`.
- `Simplify`: behält höchstens zwölf Noten in jedem gleitenden Ein-Sekunden-Fenster.

Strategien sind kombinierbare Flags. Transposition wird nur ausgeführt, wenn `AllowTransposition` gesetzt ist. Jede Modifikation und jeder ungelöste Konflikt wird protokolliert.

## Timing

Die hochpräzise VoidNote-Zeit bleibt bis zur Formatgrenze erhalten. Jeder Start wird unabhängig aus der Tempo Map in absolute Zeit umgerechnet und anschließend einmal mit `MidpointRounding.AwayFromZero` auf das Recorded-Song-V1-Raster quantisiert. Es gibt keine fortlaufende Addition gerundeter Deltas und daher keine kumulative Drift.

Kollisionen werden nie zusammengeführt oder verworfen. `Arpeggiate` darf sie innerhalb seiner Grenze nach hinten verschieben; andernfalls entsteht `ConflictUnresolved`. Der Report enthält maximalen und mittleren absoluten Quantisierungsfehler sowie die Anzahl kollidierter Events.

## ArrangementReport

Jeder Eintrag enthält Quell-ID, Quell- und Zielpitch, ursprünglichen und neuen Tick, Änderungstyp, Begründung und Strategie. Unterstützte Typen sind `Transposed`, `OctaveShift`, `PitchSubstitution`, `DroppedNote`, `Arpeggiated`, `Quantized` und `ConflictUnresolved`. Der Report zählt zudem Quellnoten, physische Ausgabeevents und ausgegebene Noten. Bei ungelösten Konflikten wird kein vermeintlich vollständiger `ShawzinTrack` zurückgegeben.

Ausgabe-IDs werden stabil aus Track-ID, Quell-IDs, Zieltimestamp und Diskriminator abgeleitet. Gleicher Input plus gleiche Optionen erzeugt damit dasselbe Arrangement und denselben Songcode.

## Multi-Shawzin-Anordnung

Vor dem bestehenden Einzeltrack-Arranger kann `IMultiShawzinSplitter` polyphone normalisierte Musik in zwei, drei, vier oder eine höhere benutzerdefinierte Stimmenzahl zerlegen. Die Split-Stufe verändert keine Pitches und quantisiert kein Timing. Danach erhält jede Stimme ihre eigene Skalenanalyse, Transpositionsrangliste, Compatibility-Bewertung und einen normalen `IShawzinArranger`-Durchlauf. Arrangement-Änderungen bleiben in den bereits definierten `ArrangementReport`-Einträgen nachvollziehbar und werden im Ensemble-Optimierungsbericht aggregiert.

Die bisherige `PreserveMelody`-Reduktion im Einzeltrack-Arranger bleibt kompatibel. Die genauere Melody-Heuristik des Splitters bewertet relative Höhe (34 %), Velocity (22 %), Dauer (16 %), Sprung-/Zeitkontinuität (20 %) und lokale Dichte (8 %). Bass-Salienz verwendet tiefes Register (42 %), Dauer (22 %), melodische Kontinuität (22 %) und rhythmische Stabilität (14 %). Details stehen in `docs/multi-shawzin.md`.

## Bekannte Einschränkungen

- Notendauern und Velocity beeinflussen den Recorded-Song-V1-Code nicht; sie bleiben nur im normalisierten Ausgabetrack erhalten.
- Melody- und Bass-Erkennung bleiben deterministische Heuristiken und keine AI- oder Partitursemantik.
- Arpeggiation verteilt nur vorwärts und erzeugt keine rhythmische Neuinterpretation.
- Manuelle Reassignment-Datenoperationen sind vorhanden; eine grafische Piano-Roll-Zuordnung bleibt späterer UI-Arbeit vorbehalten.
