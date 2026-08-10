# Shawzin-Domain und Instrumentdefinitionen

## Modellgrenzen

VoidNote trennt vier Dinge, die im Spiel gemeinsam erscheinen:

- `ShawzinNote`/`ShawzinChord`: physische Eingabe aus Saite und Fretmaske;
- `ShawzinPitchPosition`: musikalischer MIDI-artiger Pitch für genau eine physische Eingabe;
- `ShawzinScaleDefinition`: Skala, Name, Pitch Classes und alle Positionen;
- `ShawzinSoundProfile`: reine Preview-/Klangidentität.

`ShawzinDefinition` referenziert ein `ShawzinPlayProfile` und ein `ShawzinSoundProfile`. Dadurch teilen Dax und Nelumbo das eingebaute `standard-24-position`-Spielprofil, besitzen aber verschiedene Preview-Patches. Ein weiteres gleich gestimmtes Instrument benötigt keine Kopie der Mappingdaten.

## Eingebaute Daten

Milestone D liefert Dax und Nelumbo sowie alle neun im Recorded-Song-V1-Header vorhandenen Skalen. Jede Skala ordnet die 24 Kombinationen aus drei Saiten und acht Fretmasken Datenobjekten zu. Die gemeinsame C-basierte Referenz startet bei MIDI-Pitch 48. Die drei Saiten beginnen bei den Skalengraden 0, 7 und 14; überlappende Grade bilden absichtlich mehrere mögliche physische Repräsentationen ab.

Diese Projektion ist kein Codecbestandteil: Der Songcode speichert nur Skala und Eingabe. Instrument-/Tuningvarianten können später durch weitere Definitionen ergänzt werden, ohne Mapper, Analyzer oder Arranger zu ändern. Vor Veröffentlichung zusätzlicher Spielprofile müssen deren realen Tunings mit Golden Fixtures validiert werden.

## Pitch Mapping

`IShawzinPitchMapper` liefert für einen Quellpitch und eine Instrument-/Skalenwahl:

- `Exact`: mindestens eine physische Repräsentation mit identischem Pitch;
- `NotAvailable`: Pitch liegt im Skalenbereich, gehört aber nicht zu dessen darstellbaren Tönen;
- `OutsideRange`: Pitch liegt außerhalb und besitzt keine oktavgleiche Position;
- `OctaveShiftable`: mindestens eine Position derselben Pitch Class ist oktavweise erreichbar.

Alle Kandidaten sind deterministisch nach Pitch, Saite und Fret sortiert. `FindClosest` ist eine ausdrückliche Policy-Hilfe; es verändert nichts von selbst.

## Playback und Preview

`ShawzinPlaybackEngine` lädt einen geordneten `ShawzinTrack`. Alle Ziele werden relativ zu einem einzigen Timestamp von `IShawzinPlaybackScheduler` geplant. Ausgabe erfolgt über `IShawzinPlaybackOutput` als Einzelnote, Chord, Stop und Positionsänderung. Damit kann später dieselbe Transportlogik Preview- und GameBridge-Adapter versorgen, ohne selbst OS-Eingaben zu kennen.

`IShawzinPreviewRenderer` ist davon getrennt. Die erste Implementierung erzeugt deterministisch ein rechtlich unproblematisches 16-Bit-Mono-WAV mit synthetischen, abklingenden Sinustönen. Sie enthält und extrahiert keine Warframe-Samples. Die minimale Studio-UI kann dieses WAV speichern; ein plattformübergreifender Live-Audio-Geräteadapter bleibt eine spätere Erweiterung.

## GameBridge-Ausgabe

Milestone E ergänzt keine OS-Logik im Shawzin-Modul. `GameBridgePlaybackOutput` lebt ausschließlich in `VoidNote.GameBridge` und hängt sich hinter den bestehenden Playback-Port. Optional meldet es dem Transport einen kleinen `KeyDownLead`; die musikalischen Zielzeiten bleiben absolute Ziele desselben Schedulers. Hold- und Release-Zeiten ändern den nächsten Zielzeitpunkt nicht und können daher keine kumulative Drift erzeugen.

Die physische Eventstruktur wird zentral in eine `ShawzinInputAction` übersetzt. Codec, Arrangement, Instrumentdefinitionen und UI kennen keine nativen Tastenwerte. Keybinds stammen ausschließlich aus einem validierten Benutzerprofil. Sicherheits- und Plattformdetails stehen in `docs/gamebridge.md`.

## Ensembles

Milestone F ergänzt `ShawzinEnsemble` und `ShawzinEnsembleTrack` im Shawzin-Modul. Alle Mitglieder teilen dieselbe Master-Timeline, behalten aber eigene Instrumente, Skalen, Transpositionen, Compatibility Reports, Arrangement Reports, physische `ShawzinTrack`-Ergebnisse sowie Active-/Mute-/Solo-Zustände. Jeder Track bleibt dadurch einzeln analysierbar, arrangierbar, vorhörbar, codierbar und an die unveränderte GameBridge übergebbar.

Die Ensemble-Vorschau mischt ausschließlich synthetisch erzeugte Töne. Ein leichtes deterministisches Panorama und eine kleine Klangfarbenvariation aus dem `ShawzinSoundProfile` verbessern die Unterscheidbarkeit; Warframe-Audiodateien werden weder eingebettet noch extrahiert.
