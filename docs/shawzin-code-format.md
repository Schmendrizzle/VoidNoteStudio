# Warframe-Shawzin-Songcodeformat

## Geltungsbereich

Milestone C implementiert ausschließlich die von Warframe aufgezeichnete und von mehreren Community-Werkzeugen übereinstimmend beschriebene Songcode-Variante, in VoidNote **Warframe Recorded Song V1** genannt. Sie besteht aus einer Skalenkennung und festen Drei-Zeichen-Events. Andere Textnotationen von Editoren, MIDI-Konvertierungen, Playback-Modi und Game-Input sind nicht Bestandteil des Codecs.

Digital Extremes dokumentiert die Shawzin-Funktion, Skalen, Akkorde, Aufnahme, Teilen und Slow Playback öffentlich, veröffentlicht aber keine normative Bit-/Textformatspezifikation. Die folgenden Wire-Details beruhen deshalb auf unabhängig nachvollziehbaren Community-Analysen und Referenzimplementierungen. VoidNote übernimmt keinen fremden Quellcode; die Implementierung wurde neu aus der dokumentierten Struktur abgeleitet.

## Grammatik und Alphabet

```text
song-code = scale 1*event
scale     = "1" | "2" | "3" | "4" | "5" | "6" | "7" | "8" | "9"
event     = note-symbol timestamp-high timestamp-low
```

Nach dem einen Skalenzeichen muss mindestens ein vollständiges Drei-Zeichen-Event folgen. Die Zahl 4096 bezeichnet primär die Anzahl der durch 12 Bit darstellbaren Timestamp-Werte (`0..4095`), nicht ein separates Eventanzahlfeld im Code. Weil VoidNote für diese Variante strikt steigende Timestamps verlangt, kann ein kanonischer Track daraus abgeleitet höchstens 4096 Events enthalten.

Für Note und Timestamp wird das Standard-Base64-Alphabet nur als geordnetes 64-Zeichen-Ziffernalphabet verwendet:

```text
ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/
```

Es findet keine übliche Base64-Bytekodierung und keine Padding-Verarbeitung statt. Der Zeichenindex ist direkt der Wert `0..63`.

## Skalenkennung

| Zeichen | VoidNote-Wert | Skala |
| --- | --- | --- |
| `1` | `PentatonicMinor` | Pentatonisch Moll |
| `2` | `PentatonicMajor` | Pentatonisch Dur |
| `3` | `Chromatic` | Chromatisch |
| `4` | `Hexatonic` | Hexatonisch |
| `5` | `Major` | Dur |
| `6` | `Minor` | Moll |
| `7` | `Hirajoshi` | Hirajoshi |
| `8` | `Phrygian` | Phrygisch |
| `9` | `Yo` | Yo |

Der Code speichert kein Instrument oder Tuning. Das Klangprofil der ausgewählten Shawzin ist daher keine Codec-Metadatenquelle.

## Note- und Chord-Kodierung

Das erste Zeichen eines Events ist ein 6-Bit-Wert:

```text
value = (fretMask << 3) | stringMask
```

Die unteren drei Bits geben die gleichzeitig angeschlagenen Saiten an:

| Bit | VoidNote-Wert |
| --- | --- |
| `0` | `ShawzinString.First` |
| `1` | `ShawzinString.Second` |
| `2` | `ShawzinString.Third` |

Die oberen drei Bits geben die gehaltenen Fret-Tasten an:

| Bit | VoidNote-Wert |
| --- | --- |
| `0` | `ShawzinFret.Sky` |
| `1` | `ShawzinFret.Earth` |
| `2` | `ShawzinFret.Water` |

Eine einzelne Saite wird als `ShawzinNote`, mehrere Saiten unter derselben Fret-Kombination werden als `ShawzinChord` modelliert. Die Saitenmaske null erzeugt keinen Ton. Dadurch sind `A`, `I`, `Q`, `Y`, `g`, `o`, `w` und `4` keine gültigen VoidNote-Events, obwohl Warframe bzw. manche Parser sie möglicherweise still ignorieren. VoidNote lehnt sie bewusst ab, um keinen stillen Datenverlust zu erzeugen.

| Symbol | Wert | Bedeutung |
| --- | ---: | --- |
| `B` | 1 | Saite 1, kein Fret |
| `H` | 7 | alle drei Saiten, kein Fret |
| `J` | 9 | Saite 1, Sky |
| `/` | 63 | alle drei Saiten, Sky + Earth + Water |

## Timing

Die beiden Timestamp-Zeichen bilden eine 12-Bit-Zahl in Big-Endian-Reihenfolge:

```text
timestamp = alphabetIndex(timestampHigh) * 64
          + alphabetIndex(timestampLow)
```

Ein Schritt entspricht `1/16 s = 0,0625 s`. Damit ist der darstellbare Bereich:

```text
AA = 0       =   0,0000 s
AB = 1       =   0,0625 s
BA = 64      =   4,0000 s
// = 4095    = 255,9375 s
```

Der Decoder erzeugt exakte dezimale `AbsoluteTime`-Werte. `ShawzinEvent.ToMusicalTime(ProjectTimeline)` projiziert diese auf die gemeinsame VoidNote-Master-Timeline und berücksichtigt deren Tempo-Map.

Der Encoder quantisiert auf den nächsten Schritt mit `MidpointRounding.AwayFromZero`. Jede tatsächliche Änderung wird als `ShawzinTimingQuantization` im Ergebnis zurückgegeben. Führt die Quantisierung dazu, dass zwei verschiedene Events denselben Timestamp erhalten, wird der Song mit `QuantizationCollision` abgelehnt.

## Eventreihenfolge

VoidNote verlangt streng aufsteigende Timestamps. Der Decoder lehnt gleiche oder rückläufige Werte ab; der Encoder lehnt ein entsprechend unsortiertes Modell ebenfalls ab. Gleichzeitige Saiten gehören in genau ein Chord-Symbol. Diese kanonische Regel verhindert mehrdeutige Reihenfolgen und stille Zusammenfassung inkompatibler Events.

Ein erster Timestamp größer als `AA` wird als explizite Anfangspause akzeptiert. Einige frühe Analysen berichten, dass durch Warframe selbst aufgezeichnete Songs meist mit `AA` beginnen; Community-Editoren erzeugen und importieren jedoch auch spätere Startpositionen. VoidNote bewahrt den tatsächlich kodierten absoluten Wert.

## Validierung und Fehlerdiagnostik

`IShawzinCodeValidator` prüft sowohl Text als auch das interne Modell:

- nicht leere Eingabe und Skala `1..9`;
- vollständige Drei-Zeichen-Events;
- Alphabetzeichen an der korrekten Feldposition;
- klingende Saitenmaske und darstellbare Chords;
- 12-Bit-Timing und strikt aufsteigende Reihenfolge;
- strukturelle Höchstzahl von 4096 Events;
- Quantisierungsbereich und Quantisierungskollisionen.

Fehler enthalten Kategorie, nullbasierte Position im Code, problematisches Symbol und Eventindex, soweit vorhanden. Normale Parserfehler werden als `ShawzinDecodeResult` bzw. `ShawzinValidationResult` zurückgegeben und nicht als ungefangene Parser-Exception weitergereicht.

## Grenzen, Varianten und Mehrdeutigkeiten

- Das Wire-Format speichert keine Notendauer oder Velocity. Ein Event ist ein Anschlag; Nachklang ist instrumentabhängig.
- Slow Playback ist eine Warframe-Wiedergabeoption und besitzt im Code kein Variantensymbol. VoidNote interpretiert Timestamps im normalen 0,0625-Sekunden-Raster und vermischt Slow Playback nicht mit einer zweiten Zeitbasis.
- Historische Quellen nennen je nach Warframe-Version und Übertragungsweg 100, 1000 oder 1666 Noten, während neuere Community-Editoren den vollständigen 12-Bit-Wertebereich mit 4096 möglichen Timestamp-Werten (`0..4095`) nutzen. Diese Grenzen scheinen UI-, Chat-, Plattform- oder Versionsgrenzen zu sein und sind nicht selbst im Songcode kodiert. Der Codec erzwingt nur die aus dem Timestamp-Bereich und der strikt steigenden Reihenfolge abgeleitete strukturelle Grenze; ein späterer Exportkontext kann strengere Profile ergänzen.
- Einige permissive Parser akzeptieren nicht klingende Note-Symbole oder mehrdeutige Eventfolgen. VoidNote Recorded Song V1 ist absichtlich strikt und mischt diese Verhaltensvarianten nicht ein.
- Digital Extremes stellt keine normative Formatspezifikation oder Kompatibilitätsgarantie bereit. Änderungen des Spiels müssen deshalb mit neuen Golden Fixtures geprüft und bei Inkompatibilität als explizite neue Variante gekapselt werden.

## Golden Fixtures und Roundtrip

Die Offline-Fixtures liegen unter `tests/VoidNote.Shawzin.Tests/Fixtures` und umfassen Einzelnoten, mehrere Noten, Chords, minimale Abstände, maximale Pause, Randwerte, 256 Events, ungültige Zeichen, ungültiges Timing, abgeschnittene Codes, Reihenfolgefehler und nicht klingende Symbole. Ein kurzer achtstufiger Community-Referenzcode stammt aus dem technischen Beispiel der japanischen Warframe-Wiki. Tests vergleichen gültige Codes nach `Decode → Encode` bytegenau und Songs nach `Encode → Decode` semantisch.

## Recherchequellen

- Digital Extremes, [Saint of Altra: Update 25.7.0](https://www.warframe.com/en/patch-notes/pc/25-7-0): offizielle Funktionsbeschreibung zu Skalen, Chords, Aufnahme, Teilen und Slow Playback.
- Japanische Warframe-Wiki, [Shawzin](https://wikiwiki.jp/warframe/Shawzin): Zeichenalphabet, vollständige Spielsymboltabelle, Zwei-Zeichen-Timestamp und technisches Beispiel.
- Empyrrhus, [MIDI-To-Shawzin](https://github.com/Empyrrhus/MIDI-To-Shawzin): unabhängige GPL-Referenzimplementierung für Alphabet, 0,0625-Sekunden-Raster, Skalenwerte und Chordabbildungen; nur zur Gegenprüfung gelesen.
- DANser-freelancer, [Warframe-shawzin](https://github.com/DANser-freelancer/Warframe-shawzin): unabhängige AGPL-Referenzimplementierung für `1..9`, feste Eventbreite, 4096 Positionen und Note-/Chordtabellen; nur zur Gegenprüfung gelesen.
- Frühe Community-Analyse, [Decoding the Shawzin song strings](https://www.reddit.com/r/Warframe/comments/cxbpak): historische Bestätigung der 64er-Ziffern und `AA`/`BA`-Zeitbeispiele.
