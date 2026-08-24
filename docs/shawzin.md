# Shawzin-Domain und Instrumentdefinitionen

## Modellgrenzen

VoidNote trennt vier Dinge, die im Spiel gemeinsam erscheinen:

- `ShawzinNote`/`ShawzinChord`: physische Eingabe aus Saite und Fretmaske;
- `ShawzinPitchPosition`: musikalischer MIDI-artiger Pitch für genau eine physische Eingabe;
- `ShawzinScaleDefinition`: Skala, Name, Pitch Classes und alle Positionen;
- `ShawzinSoundProfile`: reine Preview-/Klangidentität.

`ShawzinDefinition` referenziert ein `ShawzinPlayProfile` und ein `ShawzinSoundProfile`. Dadurch teilen Dax und Nelumbo das eingebaute `warframe-standard-12-position-v1`-Spielprofil, besitzen aber verschiedene Preview-Patches. Ein weiteres gleich gestimmtes Instrument benötigt keine Kopie der Mappingdaten.

## Eingebaute Daten

Das reale Standardprofil enthält pro Skala genau zwölf Einzeltonpositionen. Mehrfach-Fretmasken sind Chord-/Spezialeingaben und keine weiteren Einzelton-Frets. Die Positionsreihenfolge ist:

| Index | Fret | Saite | Codesymbol |
| ---: | --- | ---: | :---: |
| 1–3 | Open | 1, 2, 3 | `B`, `C`, `E` |
| 4–6 | Sky | 1, 2, 3 | `J`, `K`, `M` |
| 7–9 | Earth | 1, 2, 3 | `R`, `S`, `U` |
| 10–12 | Water | 1, 2, 3 | `h`, `i`, `k` |

Die Tonhöhen sind Scientific Pitch Notation; C4 entspricht MIDI 60:

| Skala | Positionen 1–12 |
| --- | --- |
| Pentatonic Minor | C4, D#4, F4, G4, A#4, C5, D#5, F5, G5, A#5, C6, D#6 |
| Pentatonic Major | C4, D4, E4, G4, A4, C5, D5, E5, G5, A5, C6, D6 |
| Chromatic | C4, C#4, D4, D#4, E4, F4, F#4, G4, G#4, A4, A#4, B4 |
| Hexatonic | C4, D#4, F4, F#4, G4, A#4, C5, D#5, F5, F#5, G5, A#5 |
| Major | C4, D4, E4, F4, G4, A4, B4, C5, D5, E5, F5, G5 |
| Minor | C4, D4, D#4, F4, G4, G#4, A#4, C5, D5, D#5, F5, G5 |
| Hirajoshi | C4, C#4, F4, F#4, A#4, C5, C#5, F5, F#5, A#5, C6, C#6 |
| Phrygian Dominant | C4, C#4, E4, F4, G4, G#4, A#4, C5, C#5, E5, F5, G5 |
| Yo | C#4, D#4, F#4, G#4, A#4, C#5, D#5, F#5, G#5, A#5, C#6, D#6 |

Die frühere interne Projektion mit C3/MIDI 48, 24 Positionen, Saiten-Gradversatz und generierten Fretmasken war keine reale Warframe-Belegung. Sie war die Ursache der akzeptierten, aber musikalisch falschen Songcodes. Das neue Profil listet jede Position explizit; Algorithmus und Codec erfinden keine Tuningdaten mehr.

## Pitch Mapping

`IShawzinPitchMapper` liefert für einen Quellpitch und eine Instrument-/Skalenwahl:

- `Exact`: mindestens eine physische Repräsentation mit identischem Pitch;
- `NotAvailable`: Pitch liegt im Skalenbereich, gehört aber nicht zu dessen darstellbaren Tönen;
- `OutsideRange`: Pitch liegt außerhalb und besitzt keine oktavgleiche Position;
- `OctaveShiftable`: mindestens eine Position derselben Pitch Class ist oktavweise erreichbar.

Alle Kandidaten sind deterministisch nach Pitch, Saite und Fret sortiert. `FindClosest` ist eine ausdrückliche Policy-Hilfe; es verändert nichts von selbst.

`ReconstructPitch` führt die Gegenrichtung `String/Fret → Pitch` über dasselbe Profil aus. Der End-to-End-Test verwendet diese Funktion nach dem Decode, damit ein erfolgreicher Code-Roundtrip nicht nur physische Events, sondern auch exakt dieselben arrangierten Tonhöhen beweist.

## Quellen und Verifikation

- Warframe-Wiki, [Shawzin-Skalentabelle](https://wikiwiki.jp/warframe/Shawzin#Scale): zwölf Positionen, Oktavlagen, Fret-/Saitenreihenfolge und Codesymbole.
- Warframe Community Wiki, [Shawzin](https://warframe.fandom.com/wiki/Shawzin): unabhängige aktuelle Gegenprüfung der Skalen und des Gesamtumfangs C4–D#6.
- slimepaws, [Midi-To-Shawzin `scales.py`](https://github.com/slimepaws/Midi-To-Shawzin/blob/master/scales.py): unabhängige technische Positions-/Pitchtabelle; ein einzelner Hirajoshi-Tippfehler (`A5` statt `A#5`) wurde nicht übernommen, sondern gegen Wiki und Skalenfolge geprüft.
- Empyrrhus, [Shawzin Song Recording Syntax](https://www.reddit.com/r/Warframe/comments/cxbxoc/shawzin_song_recording_syntax/): Codezeichen, Positionsreihenfolge und nach Update 25.7.3 aktualisierte Pitchklassen.

Digital Extremes veröffentlicht keine normative Tonhöhen-/Wire-Spezifikation. Darum bleiben die Offline-Golden-Fixtures und der manuelle Ingame-12-Noten-Test die Release-Grenze; neue Spieländerungen werden als neue Profilversion modelliert.

## Playback und Preview

`ShawzinPlaybackEngine` lädt einen geordneten `ShawzinTrack`. Alle Ziele werden relativ zu einem einzigen Timestamp von `IShawzinPlaybackScheduler` geplant. Ausgabe erfolgt über `IShawzinPlaybackOutput` als Einzelnote, Chord, Stop und Positionsänderung. Damit kann später dieselbe Transportlogik Preview- und GameBridge-Adapter versorgen, ohne selbst OS-Eingaben zu kennen.

`IShawzinPreviewRenderer` ist davon getrennt. Die erste Implementierung erzeugt deterministisch ein rechtlich unproblematisches 16-Bit-Mono-WAV mit synthetischen, abklingenden Sinustönen. Sie enthält und extrahiert keine Warframe-Samples. Die minimale Studio-UI kann dieses WAV speichern; ein plattformübergreifender Live-Audio-Geräteadapter bleibt eine spätere Erweiterung.

## GameBridge-Ausgabe

Milestone E ergänzt keine OS-Logik im Shawzin-Modul. `GameBridgePlaybackOutput` lebt ausschließlich in `VoidNote.GameBridge` und hängt sich hinter den bestehenden Playback-Port. Optional meldet es dem Transport einen kleinen `KeyDownLead`; die musikalischen Zielzeiten bleiben absolute Ziele desselben Schedulers. Hold- und Release-Zeiten ändern den nächsten Zielzeitpunkt nicht und können daher keine kumulative Drift erzeugen.

Die physische Eventstruktur wird zentral in eine `ShawzinInputAction` übersetzt. Codec, Arrangement, Instrumentdefinitionen und UI kennen keine nativen Tastenwerte. Keybinds stammen ausschließlich aus einem validierten Benutzerprofil. Sicherheits- und Plattformdetails stehen in `docs/gamebridge.md`.

## Ensembles

Milestone F ergänzt `ShawzinEnsemble` und `ShawzinEnsembleTrack` im Shawzin-Modul. Alle Mitglieder teilen dieselbe Master-Timeline, behalten aber eigene Instrumente, Skalen, Transpositionen, Compatibility Reports, Arrangement Reports, physische `ShawzinTrack`-Ergebnisse sowie Active-/Mute-/Solo-Zustände. Jeder Track bleibt dadurch einzeln analysierbar, arrangierbar, vorhörbar, codierbar und an die unveränderte GameBridge übergebbar.

Die Ensemble-Vorschau mischt ausschließlich synthetisch erzeugte Töne. Ein leichtes deterministisches Panorama und eine kleine Klangfarbenvariation aus dem `ShawzinSoundProfile` verbessern die Unterscheidbarkeit; Warframe-Audiodateien werden weder eingebettet noch extrahiert.

## Feste und dynamische Skalen

Ein klassischer Warframe-Songcode enthält genau ein Skalenzeichen am Anfang. Er kann keine späteren Skalenwechsel ausdrücken. VoidNote behandelt deshalb einen `ShawzinSong` weiterhin als festen **Share Code Mode** und reicht `ShawzinScaleChangeEvent` niemals an den bestehenden Encoder weiter.

**Dynamic Ingame Mode** ist ein separates erweitertes Arrangement für GameBridge-/Live-Wiedergabe. `DynamicShawzinScalePlan` enthält skalengebundene Noten, stabile Abschnitte und explizite Wechselereignisse. `IDynamicShawzinPreviewRenderer` rekonstruiert jede physische Eingabe mit der Skala des jeweiligen Abschnitts. Damit bleibt die Vorschau das erwartete Ingame-Ergebnis. Einzelheiten stehen in [dynamic-scale-playback.md](dynamic-scale-playback.md).
