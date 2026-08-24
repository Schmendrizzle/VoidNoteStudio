# Dynamic Scale Playback

## Zwei bewusst getrennte Modi

**Share Code Mode** arrangiert den vollständigen Song in genau einer Shawzin-Skala und erzeugt den unveränderten klassischen Warframe-Songcode. Der Analyzer weist Substitutionen, Oktavwechsel, Drops und Pitchfehler dieser Einschränkung aus.

**Dynamic Ingame Mode** darf während GameBridge-/Live-Wiedergabe Skalen wechseln. Er erzeugt keinen neuen oder angeblich kompatiblen Songcode. Die UI bezeichnet ihn deshalb als **Dynamic Scale Playback – GameBridge only**, deaktiviert das Kopieren des klassischen Codes und zeigt optional einen ausdrücklich als qualitativ schwächer markierten Fixed-Fallback.

## Warframe Scale-Select-Zyklus

Scale Select schaltet ausschließlich vorwärts. Das Standardprofil verwendet `Tab`, das Binding ist jedoch frei editierbar. Die datengetriebene Reihenfolge lautet:

1. Pentatonic Minor
2. Pentatonic Major
3. Chromatic
4. Hexatonic
5. Major
6. Minor
7. Hirajoshi
8. Phrygian Dominant
9. Yo

`WarframeShawzinScaleCycle.RequiredForwardPresses` berechnet `(targetIndex - currentIndex + 9) mod 9`. Gleiche Skala kostet null, die nächste eine und die vorherige wegen Wrap-around acht Tastendrücke.

## Modellgrenze

`DynamicShawzinScalePlan` ist vom klassischen `ShawzinSong` getrennt. Er enthält:

- `DynamicShawzinNoteEvent` mit der beim Anschlag aktiven Skala;
- `ShawzinScaleChangeEvent` mit absolutem Timestamp, Quell-/Zielskala, Tastendruckzahl, Begründung, Benefit Score und Timingfenster;
- stabile `DynamicShawzinSection`-Abschnitte;
- vergleichbare Fixed-/Dynamic-Qualitätsmetriken;
- einen separat gekennzeichneten Fixed-Scale-Fallback.

Der `IShawzinCodeEncoder` akzeptiert ausschließlich `ShawzinSong`. Scale-Change-Events können seine öffentliche Schnittstelle nicht erreichen.

## Planungsalgorithmus

1. Noten werden über die gemeinsame `ProjectTimeline` geordnet und an ausreichend langen Note-Off-Pausen in musikalische Phrasen gruppiert.
2. Jeder Abschnitt wird für jede erlaubte, vom Instrument unterstützte Skala mit den normalen nachvollziehbaren Arrangement-Strategien bewertet.
3. Der Candidate Score belohnt Musical Similarity und exakte Pitches und bestraft Pitchfehler und Drops.
4. Dynamische Programmierung sucht einen Skalenpfad über ganze Abschnitte. Gleichstände bevorzugen weniger Wechsel und anschließend die stabile Skalenreihenfolge.
5. Ein Wechsel wird nur als Kante zugelassen, wenn Abschnittsstabilität, Improvement Threshold und Timing Safety erfüllt sind.
6. Ist der Gesamtvorteil gegenüber der besten festen Skala kleiner als der Schwellwert, gewinnt absichtlich der feste Plan.

Default-Penalty:

```text
TransitionPenalty = ScaleChangeCost + RequiredTabPresses × ScaleKeyPressCost
                  = 4.0 + RequiredTabPresses × 0.35
```

Die Schwellen sind Daten, keine versteckten Konstanten im Playback. Standardmäßig muss der Zielabschnitt mindestens 0,75 Sekunden und drei Noten umfassen, der Candidate Score um mindestens 3 Punkte steigen und mindestens zwei Pitchfehler-Halbtöne oder zwei Substitutionen verhindern.

## Sichere Wechsel und Timing

Ein Wechsel wird bevorzugt in einer Phrase-/Note-Off-Pause unmittelbar vor dem neuen Abschnitt geplant. Er ist nur sicher, wenn:

```text
AvailablePause >= TabPresses × (ScaleKeyPressDuration + ScaleKeyReleaseDelay)
                  + MinimumGapBeforeNextNote
```

Defaults sind 35 ms Key-Down, 25 ms Release-Abstand und 50 ms Abstand vor der nächsten Note. Reicht das Fenster nicht, wird der Wechsel verworfen. `DynamicShawzinPlaybackEngine` mischt Scale- und Notenevents nach absoluten Zielzeiten auf einem monotonic-clock-Anker; die Dauer vorheriger Tastendrücke wird nie auf den nächsten Zielzeitpunkt addiert.

Der echte Dynamic-Start verwendet denselben 3/5/10-Sekunden-Countdown wie Fixed Playback. Der Countdown liegt vollständig vor Fokusprüfung, Scheduler-Anker und Songzeit `0.000`; er wird deshalb niemals zu den absoluten Scale- oder Notenzeitpunkten addiert. Ein Wechsel bei `5.470` und eine zweite Phrase ab `6.000` bleiben auch mit 10 Sekunden Startverzögerung relativ zu Playback `0.000` exakt bei `5.470` beziehungsweise `6.000`.

## Initialskala und Sicherheit

VoidNote liest weder Warframe-Prozessspeicher noch den aktuellen Spielzustand. Vor Start zeigt die UI `Set your Shawzin to: <Scale>`. Der Benutzer stellt diese Skala manuell ein. Optional kann die ausgewählte Skala als Startvorgabe erzwungen werden; ohne Override empfiehlt der Planner die beste Startskala.

Die GameBridge verwendet ausschließlich normale abstrahierte Benutzereingaben. Es gibt keine automatische Skalen-Erkennung, Process-/Memory-API, Injection oder andere Spielautomatisierung.

## Preview, Diagnose und Metriken

Die Dynamic Preview rekonstruiert jede physische Saite/Fret-Eingabe mit der aktiven Abschnittsskala. Sie bildet deshalb die erwartete Ingame-Tonhöhe ab, nicht die Quell-MIDI-Absicht.

Analyze zeigt für Fixed und Dynamic jeweils Playability, Musical Similarity, Substitutionen, Oktavwechsel und mittleren Pitchfehler. Dynamic ergänzt Wechselzahl und gesamte TAB-Presses. Der Dry Run listet jede Note, jeden Wechsel, Abschnittsskala, erwarteten Benefit und Timing-Sicherheit; die Diagnostic Bridge sendet dabei keine realen Eingaben.

## Manuelle Prüfung

1. Zuerst die langsame Chromatic-Fixture aus [shawzin-validation.md](shawzin-validation.md) ingame bestätigen.
2. Eine eigene oder synthetische, nicht urheberrechtlich geschützte MIDI-Datei mit zwei durch eine Pause getrennten Pitchbereichen laden.
3. Analyze ausführen und Fixed-/Dynamic-Metriken vergleichen.
4. Dynamic Mode arrangieren, Preview anhören und den Dry Run auf Startskala, Wechselzeit, TAB-Zahl und `timing safe` prüfen.
5. In Warframe die angezeigte Initialskala einstellen, GameBridge bewusst armen, 5 Sekunden Startverzögerung wählen und Playback starten.
6. Während der sichtbaren Verzögerung zu Warframe wechseln; die Fokusprüfung darf erst nach dem Countdown erfolgen.
7. Prüfen, dass Songzeit `0.000` erst danach beginnt, der Wechsel vollständig in der Pause liegt und die zweite Phrase der Preview entspricht.
8. Für den RC-Regressionsplan Chromatic als Initialskala, Phrase 1 bei `0.000–4.800`, Chromatic → Pentatonic Major mit 8 TAB-Presses bei `5.470` und Phrase 2 bei `6.000–10.800` verifizieren.
9. Für einen teilbaren Code zurück zu Share Code Mode wechseln und die ausgewiesene niedrigere Fixed-Similarity akzeptieren.
